using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.EventStore;

public sealed class EventStoreReader : IEventStoreReader
{
    private readonly string _connectionString;

    public EventStoreReader(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadBatchAsync(
        long afterSequence, int batchSize, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Sequence is the clustered PK, so "everything after the bookmark,
        // in order" is a single range seek — the outbox query stays cheap no
        // matter how long the ledger grows.
        await using var command = new SqlCommand(
            """
            SELECT TOP (@BatchSize) Sequence, AggregateId, Version, EventType, EventData, OccurredAtUtc
            FROM EventStore
            WHERE Sequence > @AfterSequence
            ORDER BY Sequence
            """, connection);
        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@AfterSequence", afterSequence);

        var batch = new List<StoredEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            batch.Add(new StoredEvent(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDateTime(5)));
        }

        return batch;
    }
}
