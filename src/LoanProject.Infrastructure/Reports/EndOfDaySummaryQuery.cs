using System.Data;
using Dapper;
using LoanProject.Application.Reports;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.Reports;

/// <summary>
/// Calls usp_GetEndOfDayLoanSummary through Dapper: the SQL lives in the
/// database, row-to-object mapping is Dapper's job — the middle ground
/// between raw ADO.NET (the ledger) and full EF (CRUD).
/// </summary>
public sealed class EndOfDaySummaryQuery : IEndOfDaySummaryQuery
{
    private readonly string _connectionString;

    public EndOfDaySummaryQuery(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<EndOfDayLoanSummary>> GetAsync(DateOnly asOfDate, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);

        var command = new CommandDefinition(
            "dbo.usp_GetEndOfDayLoanSummary",
            new { AsOfDate = asOfDate.ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        // Dapper matches result columns to the record's constructor
        // parameters by name — no mapping code, no change tracking.
        var rows = await connection.QueryAsync<EndOfDayLoanSummary>(command);
        return rows.AsList();
    }
}
