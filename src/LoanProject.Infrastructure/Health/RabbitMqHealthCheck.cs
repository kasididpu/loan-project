using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace LoanProject.Infrastructure.Health;

/// <summary>
/// Readiness probe for RabbitMQ: opens a short-lived connection. The broker backs
/// the async payment-notification path; a replica that cannot reach it should
/// fall out of rotation so a webhook publish never blocks the caller.
/// </summary>
public sealed class RabbitMqHealthCheck(string connectionString) : IHealthCheck
{
    // RabbitMQ.Client 6.x connects synchronously; the probe is wrapped in a
    // completed task to satisfy the async IHealthCheck contract.
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
            };

            using var connection = factory.CreateConnection();
            return Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection did not open."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ is unreachable.", exception));
        }
    }
}
