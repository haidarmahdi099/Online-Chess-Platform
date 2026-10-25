using Analysis.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Analysis.Api.Health;

public sealed class EngineHealthCheck(IChessEngine engine) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            return await engine.IsAvailableAsync(timeout.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Chess engine is unavailable.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "Chess engine did not respond in time.");
        }
    }
}
