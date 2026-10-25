using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Rating.Api.Health;

public sealed class KafkaHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bootstrapServers =
                configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            using var adminClient = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = bootstrapServers })
                .Build();

            await Task.Run(
                () => adminClient.GetMetadata(TimeSpan.FromSeconds(3)),
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Kafka broker is unavailable.", exception);
        }
    }
}
