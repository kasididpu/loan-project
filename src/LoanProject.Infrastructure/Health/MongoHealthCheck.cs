using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoanProject.Infrastructure.Health;

/// <summary>
/// Readiness probe for MongoDB: runs the admin <c>ping</c> command through the
/// shared client. Mongo backs the audit log and flexible-schema loan
/// applications, so a replica that cannot reach it is not ready to serve.
/// </summary>
public sealed class MongoHealthCheck(IMongoClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unreachable.", exception);
        }
    }
}
