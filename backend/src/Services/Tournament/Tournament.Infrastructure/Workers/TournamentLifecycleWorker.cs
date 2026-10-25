using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tournament.Application.Services;

namespace Tournament.Infrastructure.Workers;

/// <summary>
/// Drives tournaments forward without anyone calling an endpoint: starts tournaments when
/// their start time arrives, advances rounds that finished while no event was processed, and
/// keeps retrying game creation until the Game service has accepted every pairing.
/// </summary>
public sealed class TournamentLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<TournamentLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = int.TryParse(configuration["Tournament:MaintenanceIntervalSeconds"], out var parsed)
            ? Math.Clamp(parsed, 2, 300)
            : 10;

        logger.LogInformation("Tournament lifecycle worker started (every {Seconds}s).", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ITournamentService>();
                await service.RunMaintenanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tournament maintenance cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
