using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tournament.Application.Interfaces;
using Tournament.Application.Services;

namespace Tournament.Infrastructure.Messaging;

/// <summary>
/// Applies finished games to tournament matches. The offset is committed only after the
/// result is stored (at-least-once); recording is idempotent, so redelivery is harmless.
/// A failed message is retried in place (Seek) instead of being skipped.
/// </summary>
public sealed class GameFinishedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<GameFinishedConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var topic = configuration["Kafka:GameFinishedTopic"] ?? "game.finished";
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = configuration["Kafka:ConsumerGroupId"] ?? "tournament-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);
        logger.LogInformation("Tournament game-finished consumer started on {Topic}.", topic);

        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message is null) continue;

                await HandleAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume failure in the Tournament service.");
                await DelayAsync(++consecutiveFailures, stoppingToken);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Invalid game-finished event; skipping it.");
                if (result is not null) consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not apply game-finished event; it will be retried.");
                if (result is not null)
                {
                    try { consumer.Seek(result.TopicPartitionOffset); }
                    catch (Exception seekEx) { logger.LogWarning(seekEx, "Could not rewind to the failed event."); }
                }
                await DelayAsync(++consecutiveFailures, stoppingToken);
            }
        }

        try { consumer.Close(); } catch { /* shutting down */ }
    }

    private async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<GameFinishedMessage>(payload, JsonOptions);
        if (message is null || message.GameId == Guid.Empty || string.IsNullOrWhiteSpace(message.Result))
            throw new JsonException("Game-finished event is missing its game id or result.");

        var finishedAt = message.OccurredAtUtc == default
            ? DateTime.UtcNow
            : message.OccurredAtUtc.Kind == DateTimeKind.Utc
                ? message.OccurredAtUtc
                : DateTime.SpecifyKind(message.OccurredAtUtc, DateTimeKind.Utc);

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITournamentService>();
        var outcome = await service.RecordGameResultAsync(message.GameId, message.Result, finishedAt, cancellationToken);

        switch (outcome.Status)
        {
            case MatchResultStatus.Recorded:
                logger.LogInformation(
                    "Recorded {Result} for tournament {TournamentId}, game {GameId} ({Advance}).",
                    message.Result, outcome.TournamentId, message.GameId, outcome.Advance);
                break;
            case MatchResultStatus.InvalidResult:
            case MatchResultStatus.InvalidState:
            case MatchResultStatus.MatchNotFound:
                logger.LogWarning(
                    "Ignored game-finished event for game {GameId}: {Status}.",
                    message.GameId, outcome.Status);
                break;
        }
    }

    private static Task DelayAsync(int failures, CancellationToken cancellationToken)
    {
        var seconds = Math.Min(30, 2 * Math.Max(1, failures));
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }

    private sealed record GameFinishedMessage(
        Guid GameId,
        string Result,
        DateTime OccurredAtUtc);
}
