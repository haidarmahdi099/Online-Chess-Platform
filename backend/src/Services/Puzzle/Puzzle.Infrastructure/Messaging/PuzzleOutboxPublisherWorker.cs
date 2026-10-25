using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Puzzle.Infrastructure.Persistence;

namespace Puzzle.Infrastructure.Messaging;

/// <summary>
/// Publishes durable puzzle-attempt outbox records. If the process stops after Kafka
/// accepts a message but before PublishedAtUtc is saved, the event may be delivered again;
/// consumers must deduplicate by the stable EventId.
/// </summary>
public sealed class PuzzleOutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PuzzleOutboxPublisherWorker> logger) : BackgroundService
{
    private readonly string _bootstrapServers =
        configuration["Kafka:BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured.");
    private readonly string _topic = configuration["Kafka:PuzzleAttemptedTopic"] ?? "puzzle.attempted";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var delayMs = Math.Clamp(configuration.GetValue("Kafka:OutboxPollIntervalMs", 1000), 250, 30000);
        var batchSize = Math.Clamp(configuration.GetValue("Kafka:OutboxBatchSize", 50), 1, 500);

        logger.LogInformation("Puzzle outbox publisher started for topic {Topic}.", _topic);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publishedAny = await PublishBatchAsync(producer, batchSize, stoppingToken);
                if (!publishedAny)
                    await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Puzzle outbox batch failed; pending events will be retried.");
                await Task.Delay(delayMs, stoppingToken);
            }
        }

        producer.Flush(TimeSpan.FromSeconds(5));
        logger.LogInformation("Puzzle outbox publisher stopped.");
    }

    private async Task<bool> PublishBatchAsync(
        IProducer<string, string> producer,
        int batchSize,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
        var pending = await db.OutboxMessages
            .Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return false;

        var encounteredFailure = false;
        foreach (var item in pending)
        {
            try
            {
                await producer.ProduceAsync(_topic, new Message<string, string>
                {
                    Key = item.EventId.ToString(),
                    Value = item.Payload
                }, cancellationToken);
                item.PublishedAtUtc = DateTime.UtcNow;
                item.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.Attempts++;
                item.LastError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
                logger.LogError(ex, "Failed publishing puzzle outbox event {EventId}; attempt {Attempts}.", item.EventId, item.Attempts);
                encounteredFailure = true;
                // Preserve ordering: stop the batch on the first failure and retry later.
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return !encounteredFailure;
    }
}
