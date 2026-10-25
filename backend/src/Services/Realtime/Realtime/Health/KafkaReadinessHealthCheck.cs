using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Realtime.Health;

public sealed class KafkaReadinessHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        try
        {
            var metadata = await Task.Run(() =>
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = bootstrapServers
                }).Build();
                return admin.GetMetadata(TimeSpan.FromSeconds(2));
            }, cancellationToken);

            return metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy("Kafka broker metadata is available.")
                : HealthCheckResult.Unhealthy("Kafka returned no broker metadata.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Kafka is unavailable.", exception);
        }
    }
}
