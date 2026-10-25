using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Realtime.Health;

public sealed class RedisReadinessHealthCheck(IConnectionMultiplexer? redis = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (redis is null)
            return Task.FromResult(HealthCheckResult.Healthy("Redis backplane is not configured."));

        return Task.FromResult(redis.IsConnected
            ? HealthCheckResult.Healthy("Redis backplane is connected.")
            : HealthCheckResult.Unhealthy("Redis backplane is disconnected."));
    }
}
