using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace LoanProject.Infrastructure.Health;

/// <summary>
/// Readiness probe for Redis: PINGs the shared multiplexer. The multiplexer is
/// created with AbortOnConnectFail off (so the app boots with Redis down), which
/// is exactly why an explicit ping is needed to tell "ready" from "degraded".
/// </summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", exception);
        }
    }
}
