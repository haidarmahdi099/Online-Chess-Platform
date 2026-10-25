using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tournament.Infrastructure.Persistence;

namespace Tournament.Infrastructure.Messaging;

/// <summary>At-least-once dispatcher for persisted tournament events. Consumers should be idempotent.</summary>
public sealed class TournamentOutboxDispatcher(IServiceScopeFactory scopes, ITournamentEventPublisher publisher, ILogger<TournamentOutboxDispatcher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                // Claim one row with a conditional update so multiple service replicas do not
                // normally publish the same pending row concurrently. Expired claims are retried.
                var now = DateTime.UtcNow;
                var row = await db.OutboxMessages.AsNoTracking().Where(x => x.Status == "Pending" || (x.Status == "Processing" && x.UpdatedAtUtc < now.AddMinutes(-2)))
                    .OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(stoppingToken);
                if (row is null) { await Task.Delay(TimeSpan.FromMilliseconds(750), stoppingToken); continue; }
                var claimed = await db.OutboxMessages.Where(x => x.EventId == row.EventId && (x.Status == "Pending" || (x.Status == "Processing" && x.UpdatedAtUtc < now.AddMinutes(-2))))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Processing").SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.Attempts, x => x.Attempts + 1), stoppingToken);
                if (claimed == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(row.DataJson);
                    await publisher.PublishAsync(row.EventType, row.TournamentId, doc.RootElement.Clone(), stoppingToken);
                    await db.OutboxMessages.Where(x => x.EventId == row.EventId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Published").SetProperty(x => x.PublishedAtUtc, DateTime.UtcNow).SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow).SetProperty(x => x.LastError, (string?)null), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to publish tournament outbox event {EventId}", row.EventId);
                    var delaySeconds = Math.Min(60, Math.Pow(2, Math.Min(row.Attempts + 1, 6)));
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                    await db.OutboxMessages.Where(x => x.EventId == row.EventId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Pending").SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow).SetProperty(x => x.LastError, ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Tournament outbox dispatcher iteration failed"); await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
        }
    }
}
