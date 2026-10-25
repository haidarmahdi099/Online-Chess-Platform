using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Messaging;

/// <summary>Publishes outbox messages to Kafka; the topic is chosen from the outbox event type.</summary>
public sealed class GameFinishedEventPublisher : IDisposable
{
    private const string GameFinishedEventType = "game.finished";
    private const string GameAbortedEventType = "game.aborted";
    private const string GameMatchedEventType = "game.matched";

    private readonly IProducer<string, string> _producer;
    private readonly string _finishedTopic;
    private readonly string _abortedTopic;
    private readonly string _matchedTopic;
    private readonly ILogger<GameFinishedEventPublisher> _logger;

    public GameFinishedEventPublisher(
        IConfiguration configuration,
        ILogger<GameFinishedEventPublisher> logger)
    {
        var bootstrapServers =
            configuration["Kafka:BootstrapServers"];

        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            throw new InvalidOperationException(
                "Kafka:BootstrapServers is not configured.");
        }

        _finishedTopic = configuration["Kafka:GameFinishedTopic"]
            ?? "game.finished";
        _abortedTopic = configuration["Kafka:GameAbortedTopic"]
            ?? "game.aborted";
        _matchedTopic = configuration["Kafka:GameMatchedTopic"]
            ?? "game.matched";
        _logger = logger;

        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = 10000
            })
            .Build();
    }

    public async Task PublishAsync(
        string eventType,
        Guid eventId,
        string payload,
        CancellationToken cancellationToken)
    {
        // Unknown event types must never fall through to the finished topic: Rating and
        // Tournament consume it and would treat the message as a finished game.
        var topic = eventType switch
        {
            GameFinishedEventType => _finishedTopic,
            GameAbortedEventType => _abortedTopic,
            GameMatchedEventType => _matchedTopic,
            _ => throw new InvalidOperationException($"Unsupported game outbox event type '{eventType}'.")
        };

        var message = new Message<string, string>
        {
            Key = eventId.ToString(),
            Value = payload
        };

        try
        {
            await _producer.ProduceAsync(
                topic,
                message,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish outbox event {EventId} to {Topic}.",
                eventId,
                topic);

            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
