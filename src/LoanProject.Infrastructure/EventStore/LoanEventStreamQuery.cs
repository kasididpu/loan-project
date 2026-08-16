using LoanProject.Application.Loans;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// Reads a single loan's events by aggregate id, ordered by version — the audit
/// trail behind GET /loans/{id}/events. Read-only; the ledger is append-only.
/// </summary>
public sealed class LoanEventStreamQuery : ILoanEventStreamQuery
{
    private readonly string _connectionString;

    public LoanEventStreamQuery(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<LoanEventEntry>> GetAsync(Guid loanId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Seek on (AggregateId, Version) via the unique index — the whole stream
        // for one loan, in the order it happened.
        await using var command = new SqlCommand(
            """
            SELECT Version, EventType, EventData, OccurredAtUtc
            FROM EventStore
            WHERE AggregateId = @AggregateId
            ORDER BY Version
            """, connection);
        command.Parameters.AddWithValue("@AggregateId", loanId);

        var events = new List<LoanEventEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new LoanEventEntry(
                reader.GetInt32(0),   // Version
                reader.GetString(1),  // EventType
                reader.GetDateTime(3),// OccurredAtUtc
                reader.GetString(2)));// EventData (JSON)
        }

        return events;
    }
}
