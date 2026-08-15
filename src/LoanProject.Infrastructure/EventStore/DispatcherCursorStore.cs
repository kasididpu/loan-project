using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.EventStore;

public sealed class DispatcherCursorStore : IDispatcherCursorStore
{
    private readonly string _connectionString;

    public DispatcherCursorStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<long> GetLastSequenceAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "SELECT LastSequence FROM DispatcherCursor WHERE Id = 1", connection);

        var lastSequence = await command.ExecuteScalarAsync(cancellationToken);
        return lastSequence is null ? 0L : (long)lastSequence;
    }

    public async Task AdvanceAsync(long lastSequence, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Update-then-insert upsert, same shape as the snapshot writer. The
        // CK_DispatcherCursor_SingleRow check keeps this a singleton row.
        await using var command = new SqlCommand(
            """
            UPDATE DispatcherCursor SET LastSequence = @LastSequence WHERE Id = 1;

            IF @@ROWCOUNT = 0
                INSERT INTO DispatcherCursor (Id, LastSequence) VALUES (1, @LastSequence);
            """, connection);
        command.Parameters.AddWithValue("@LastSequence", lastSequence);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
