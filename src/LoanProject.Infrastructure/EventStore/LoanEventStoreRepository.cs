using System.Text.Json;
using System.Text.Json.Serialization;
using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;
using LoanProject.Domain.Loans.Events;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// The only door to the ledger. Talks to the EventStore/LoanSnapshot tables
/// with hand-written parameterized SQL — no DbSet, no change tracker: those
/// exist to UPDATE, and the ledger is append-only.
/// </summary>
public sealed class LoanEventStoreRepository : ILoanRepository
{
    // Roadmap v5 decision: typical streams are ≤70 events, so a plain replay
    // is already fast — the interval exists to exercise the full mechanism.
    private const int SnapshotInterval = 25;

    // Both numbers mean "another writer inserted this (AggregateId, Version)
    // first": 2627 = unique constraint violation, 2601 = duplicate key row.
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateKeyRow = 2601;

    // Same contract as the event payloads: enums stored as names, options
    // instance reused so its type cache survives across calls.
    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString;

    public LoanEventStoreRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<Loan?> LoadAsync(Guid loanId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var snapshot = await ReadSnapshotAsync(connection, loanId, cancellationToken);
        var fromVersion = snapshot?.Version ?? 0;

        // The tail: everything the snapshot has not seen yet. The seek on
        // (AggregateId, Version) rides the UQ_EventStore_AggVer index.
        var tail = new List<IDomainEvent>();
        await using (var command = new SqlCommand(
            """
            SELECT EventType, EventData
            FROM EventStore
            WHERE AggregateId = @AggregateId AND Version > @FromVersion
            ORDER BY Version
            """, connection))
        {
            command.Parameters.AddWithValue("@AggregateId", loanId);
            command.Parameters.AddWithValue("@FromVersion", fromVersion);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                tail.Add(LoanEventSerializer.Deserialize(reader.GetString(0), reader.GetString(1)));
        }

        if (snapshot is null && tail.Count == 0)
            return null;

        return snapshot is null
            ? Loan.LoadFromHistory(tail)
            : Loan.FromSnapshot(snapshot, tail);
    }

    public async Task SaveAsync(Loan loan, CancellationToken cancellationToken)
    {
        if (loan.UncommittedEvents.Count == 0)
            return;

        // The version the aggregate had when it was loaded — every new event
        // sits on top of it. If another writer appended meanwhile, the first
        // INSERT below collides with UQ_EventStore_AggVer and the whole
        // transaction rolls back: all events land, or none do.
        var expectedVersion = loan.Version - loan.UncommittedEvents.Count;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var version = expectedVersion;
            foreach (var domainEvent in loan.UncommittedEvents)
            {
                version++;
                var (eventType, eventData) = LoanEventSerializer.Serialize(domainEvent);

                await using var command = new SqlCommand(
                    """
                    INSERT INTO EventStore (AggregateId, Version, EventType, EventData, OccurredAtUtc)
                    VALUES (@AggregateId, @Version, @EventType, @EventData, @OccurredAtUtc)
                    """, connection, transaction);
                command.Parameters.AddWithValue("@AggregateId", loan.Id);
                command.Parameters.AddWithValue("@Version", version);
                command.Parameters.AddWithValue("@EventType", eventType);
                command.Parameters.AddWithValue("@EventData", eventData);
                command.Parameters.AddWithValue("@OccurredAtUtc", domainEvent.OccurredAtUtc);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // Integer division spots a crossed interval boundary regardless of
            // batch size: 24 -> 26 crosses 25 (0 -> 1), 26 -> 27 does not.
            if (loan.Version / SnapshotInterval > expectedVersion / SnapshotInterval)
                await WriteSnapshotAsync(connection, transaction, loan, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is UniqueConstraintViolation or DuplicateKeyRow)
        {
            throw new LoanConcurrencyException(loan.Id, expectedVersion, exception);
        }

        // Only after a successful commit: the facts are now in the ledger.
        loan.ClearUncommittedEvents();
    }

    private static async Task<LoanSnapshotState?> ReadSnapshotAsync(
        SqlConnection connection, Guid loanId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT StateData FROM LoanSnapshot WHERE AggregateId = @AggregateId", connection);
        command.Parameters.AddWithValue("@AggregateId", loanId);

        var stateData = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return stateData is null
            ? null
            : JsonSerializer.Deserialize<LoanSnapshotState>(stateData, SnapshotJson);
    }

    private static async Task WriteSnapshotAsync(
        SqlConnection connection, SqlTransaction transaction, Loan loan, CancellationToken cancellationToken)
    {
        var stateData = JsonSerializer.Serialize(loan.ToSnapshot(), SnapshotJson);

        // Update-then-insert upsert: the snapshot is a single-row cache per
        // loan (overwriting is fine — it is not the ledger), and this shape
        // is easier to reason about than MERGE's edge cases.
        await using var command = new SqlCommand(
            """
            UPDATE LoanSnapshot
            SET Version = @Version, StateData = @StateData, TakenAtUtc = @TakenAtUtc
            WHERE AggregateId = @AggregateId;

            IF @@ROWCOUNT = 0
                INSERT INTO LoanSnapshot (AggregateId, Version, StateData, TakenAtUtc)
                VALUES (@AggregateId, @Version, @StateData, @TakenAtUtc);
            """, connection, transaction);
        command.Parameters.AddWithValue("@AggregateId", loan.Id);
        command.Parameters.AddWithValue("@Version", loan.Version);
        command.Parameters.AddWithValue("@StateData", stateData);
        command.Parameters.AddWithValue("@TakenAtUtc", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
