using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Reader against the real EventStore table. Other test classes append
/// concurrently, so assertions filter to this test's own aggregate instead
/// of expecting an exclusive view of the global sequence.
/// </summary>
public class EventStoreReaderTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<long> CurrentMaxSequenceAsync()
    {
        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT ISNULL(MAX(Sequence), 0) FROM EventStore", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task ReadBatchAsync_EventsAfterSequence_ReturnsThemInWriteOrder()
    {
        var beforeAppend = await CurrentMaxSequenceAsync();
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), 100_000m, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);
        loan.Disburse(100_000m, Now);
        await loanRepository.SaveAsync(loan, CancellationToken.None);

        var batch = await new EventStoreReader(TestDatabase.ConnectionString)
            .ReadBatchAsync(beforeAppend, batchSize: 1_000, CancellationToken.None);

        var mine = batch.Where(storedEvent => storedEvent.AggregateId == loan.Id).ToList();
        Assert.Equal(3, mine.Count);
        Assert.Equal(new[] { 1, 2, 3 }, mine.Select(storedEvent => storedEvent.Version));
        Assert.Equal(
            new[] { "LoanOriginated", "LoanApproved", "LoanDisbursed" },
            mine.Select(storedEvent => storedEvent.EventType));
        // Rows come back in global write order, and payloads ride along raw.
        Assert.True(batch.SequenceEqual(batch.OrderBy(storedEvent => storedEvent.Sequence)));
        Assert.Contains("\"Principal\":100000", mine[0].EventData.Replace(" ", ""));
    }

    [Fact]
    public async Task ReadBatchAsync_BatchSize_CapsTheWindow()
    {
        var beforeAppend = await CurrentMaxSequenceAsync();
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), 100_000m, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);
        loan.Disburse(100_000m, Now);
        await loanRepository.SaveAsync(loan, CancellationToken.None);

        var batch = await new EventStoreReader(TestDatabase.ConnectionString)
            .ReadBatchAsync(beforeAppend, batchSize: 2, CancellationToken.None);

        Assert.Equal(2, batch.Count);
    }
}
