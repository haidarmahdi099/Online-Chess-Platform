using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Realtime.Health;

public sealed class GameApiReadinessHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient("GameApi")
                .GetAsync("health/ready", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Game API is ready.")
                : HealthCheckResult.Unhealthy($"Game API readiness returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Game API is unavailable.", exception);
        }
    }
}
