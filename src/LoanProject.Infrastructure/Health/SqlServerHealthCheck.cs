using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanProject.Infrastructure.Health;

/// <summary>
/// Readiness probe for a SQL Server / Azure SQL Database: opens a connection and
/// runs <c>SELECT 1</c>. One instance guards the write database and another the
/// read database, so a replica reports "not ready" — and the load balancer stops
/// routing to it — whenever either database is unreachable.
/// </summary>
public sealed class SqlServerHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unreachable.", exception);
        }
    }
}
