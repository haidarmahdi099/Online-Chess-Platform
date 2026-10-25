using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rating.Application.Events;
using Rating.Application.Services;

namespace Rating.Infrastructure.Messaging;

public sealed class RatingEventConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RatingEventConsumer> logger)
    : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var bootstrapServers =
            configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

        var groupId =
            configuration["Kafka:ConsumerGroupId"] ?? "rating-service";

        var gameFinishedTopic =
            configuration["Kafka:GameFinishedTopic"] ?? "game.finished";

        var puzzleAttemptedTopic =
            configuration["Kafka:PuzzleAttemptedTopic"] ?? "puzzle.attempted";
        var deadLetterTopic =
            configuration["Kafka:DeadLetterTopic"] ?? "rating.deadletter";
        var maxAttempts =
            configuration.GetValue("Kafka:MaxProcessingAttempts", 5);
        var retryDelay = TimeSpan.FromSeconds(
            configuration.GetValue("Kafka:RetryDelaySeconds", 2));

        if (maxAttempts < 1)
        {
            throw new InvalidOperationException(
                "Kafka:MaxProcessingAttempts must be at least 1.");
        }

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe([gameFinishedTopic, puzzleAttemptedTopic]);

        logger.LogInformation(
            "Rating Kafka consumer started. Brokers: {Brokers}; group: {GroupId}",
            bootstrapServers,
            groupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string> result;

            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            var processingError = await ProcessWithRetriesAsync(
                result,
                gameFinishedTopic,
                puzzleAttemptedTopic,
                maxAttempts,
                retryDelay,
                stoppingToken);

            if (processingError is not null)
            {
                try
                {
                    var envelope = new DeadLetterEnvelope(
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Key,
                        result.Message.Value ?? string.Empty,
                        processingError.GetType().FullName
                            ?? processingError.GetType().Name,
                        processingError.Message,
                        DateTimeOffset.UtcNow,
                        maxAttempts);

                    await producer.ProduceAsync(
                        deadLetterTopic,
                        new Message<string, string>
                        {
                            Key = result.Message.Key
                                ?? $"{result.Topic}:{result.Partition.Value}:{result.Offset.Value}",
                            Value = JsonSerializer.Serialize(envelope, _jsonOptions)
                        },
                        stoppingToken);

                    logger.LogError(
                        processingError,
                        "Event from {Topic}, partition {Partition}, offset {Offset} " +
                        "failed after {Attempts} attempts and was sent to {DeadLetterTopic}.",
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        maxAttempts,
                        deadLetterTopic);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Could not dead-letter event at {TopicPartitionOffset}; " +
                        "seeking back to retry it.",
                        result.TopicPartitionOffset);

                    SeekToCurrent(consumer, result);
                    await Task.Delay(retryDelay, stoppingToken);
                    continue;
                }
            }

            try
            {
                // Commit only after successful processing or confirmed DLQ delivery.
                consumer.Commit(result);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not commit {TopicPartitionOffset}; seeking back to retry it.",
                    result.TopicPartitionOffset);

                SeekToCurrent(consumer, result);
                await Task.Delay(retryDelay, stoppingToken);
            }
        }

        logger.LogInformation("Rating Kafka consumer stopped.");
    }

    private async Task<Exception?> ProcessWithRetriesAsync(
        ConsumeResult<string, string> result,
        string gameFinishedTopic,
        string puzzleAttemptedTopic,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var ratingService =
                    scope.ServiceProvider.GetRequiredService<IRatingService>();

                switch (result.Topic)
                {
                    case var topic when topic == gameFinishedTopic:
                    {
                        var gameEvent =
                            JsonSerializer.Deserialize<GameFinishedEvent>(
                                result.Message.Value, _jsonOptions)
                            ?? throw new JsonException(
                                "Could not deserialize GameFinishedEvent.");

                        if (gameEvent.Rated)
                        {
                            await ratingService.ProcessGameFinishedAsync(
                                gameEvent, cancellationToken);
                        }

                        return null;
                    }

                    case var topic when topic == puzzleAttemptedTopic:
                    {
                        var puzzleEvent =
                            JsonSerializer.Deserialize<PuzzleAttemptedEvent>(
                                result.Message.Value, _jsonOptions)
                            ?? throw new JsonException(
                                "Could not deserialize PuzzleAttemptedEvent.");

                        await ratingService.ProcessPuzzleAttemptedAsync(
                            puzzleEvent, cancellationToken);

                        return null;
                    }

                    default:
                        throw new InvalidOperationException(
                            $"Unexpected Kafka topic: {result.Topic}");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Processing {TopicPartitionOffset} failed on attempt {Attempt}/{MaxAttempts}.",
                    result.TopicPartitionOffset,
                    attempt,
                    maxAttempts);

                if (attempt == maxAttempts)
                {
                    return exception;
                }

                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        return new InvalidOperationException(
            "Kafka event processing ended without a result.");
    }

    private void SeekToCurrent(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result)
    {
        try
        {
            consumer.Seek(result.TopicPartitionOffset);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not seek to {TopicPartitionOffset}.",
                result.TopicPartitionOffset);
        }
    }

    private sealed record DeadLetterEnvelope(
        string OriginalTopic,
        int Partition,
        long Offset,
        string? Key,
        string Payload,
        string ExceptionType,
        string Error,
        DateTimeOffset FailedAtUtc,
        int Attempts);
}
