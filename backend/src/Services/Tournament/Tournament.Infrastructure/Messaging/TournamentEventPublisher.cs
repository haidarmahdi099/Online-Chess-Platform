using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Tournament.Infrastructure.Messaging;

public interface ITournamentEventPublisher
{
    Task PublishAsync(string eventType, Guid tournamentId, object? data = null, CancellationToken cancellationToken = default);
}

public sealed class TournamentEventPublisher : ITournamentEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    public TournamentEventPublisher(IConfiguration configuration)
    {
        _topic = configuration["Kafka:TournamentEventsTopic"] ?? "tournament.events";
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        { BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092", Acks = Acks.All, EnableIdempotence = true }).Build();
    }
    public async Task PublishAsync(string eventType, Guid tournamentId, object? data = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { eventType, tournamentId, occurredAtUtc = DateTime.UtcNow, data }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await _producer.ProduceAsync(_topic, new Message<string, string> { Key = tournamentId.ToString(), Value = payload }, cancellationToken);
    }
    public void Dispose() => _producer.Dispose();
}
