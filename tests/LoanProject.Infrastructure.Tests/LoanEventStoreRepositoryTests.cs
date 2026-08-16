using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using Microsoft.Data.SqlClient;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The full ledger round trip against real SQL Server: append with
/// expected version, reload via snapshot + tail replay, snapshot every
/// 25 events. Concurrency conflicts get their own suite in the next slice.
/// </summary>
public class LoanEventStoreRepositoryTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static LoanEventStoreRepository CreateRepository() => new(TestDatabase.ConnectionString);

    private static Loan NewActiveLoan(int termMonths = 12)
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, termMonths, Now);
        loan.Approve(Guid.NewGuid(), "officer-1", Now);
        loan.Disburse(Principal, Guid.NewGuid(), "officer", Now);
        return loan;
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RebuildsIdenticalState()
    {
        var repository = CreateRepository();
        var original = NewActiveLoan();
        original.ReceivePayment(Guid.NewGuid(), original.Schedule![0].Payment, 1, "evt_it_1", Now);

        await repository.SaveAsync(original, CancellationToken.None);
        var loaded = await repository.LoadAsync(original.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(original.Id, loaded!.Id);
        Assert.Equal(original.Status, loaded.Status);
        Assert.Equal(original.OutstandingBalance, loaded.OutstandingBalance);
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(original.NextInstallmentNo, loaded.NextInstallmentNo);
        Assert.Equal(original.Schedule, loaded.Schedule);
        Assert.Empty(loaded.UncommittedEvents);
    }

    [Fact]
    public async Task LoadAsync_UnknownLoan_ReturnsNull()
    {
        var loaded = await CreateRepository().LoadAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveAsync_Succeeding_ClearsUncommittedEvents()
    {
        var repository = CreateRepository();
        var loan = NewActiveLoan();

        await repository.SaveAsync(loan, CancellationToken.None);

        Assert.Empty(loan.UncommittedEvents);
    }

    [Fact]
    public async Task SaveAsync_IncrementalSaves_AppendToTheSameStream()
    {
        var repository = CreateRepository();
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        await repository.SaveAsync(loan, CancellationToken.None);

        loan.Approve(Guid.NewGuid(), "officer-1", Now);
        await repository.SaveAsync(loan, CancellationToken.None);
        loan.Disburse(Principal, Guid.NewGuid(), "officer", Now);
        await repository.SaveAsync(loan, CancellationToken.None);

        var loaded = await repository.LoadAsync(loan.Id, CancellationToken.None);
        Assert.Equal(LoanStatus.Active, loaded!.Status);
        Assert.Equal(3, loaded.Version);
    }

    [Fact]
    public async Task SaveAsync_CrossingSnapshotInterval_WritesSnapshotRow()
    {
        var repository = CreateRepository();
        // 24-month lifecycle: originate + approve + disburse + 24 payments
        // + settle = 28 events — crosses the interval of 25 exactly once.
        var loan = NewActiveLoan(termMonths: 24);
        var lastPaymentId = Guid.Empty;
        foreach (var row in loan.Schedule!)
        {
            lastPaymentId = Guid.NewGuid();
            loan.ReceivePayment(lastPaymentId, row.Payment, row.Number, $"evt_it_{row.Number}", Now);
        }
        loan.Settle(lastPaymentId, Now);

        await repository.SaveAsync(loan, CancellationToken.None);

        Assert.Equal(28, await ReadSnapshotVersionAsync(loan.Id));
        var loaded = await repository.LoadAsync(loan.Id, CancellationToken.None);
        Assert.Equal(LoanStatus.Settled, loaded!.Status);
        Assert.Equal(0m, loaded.OutstandingBalance);
        Assert.Equal(28, loaded.Version);
    }

    private static async Task<int?> ReadSnapshotVersionAsync(Guid loanId)
    {
        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT Version FROM LoanSnapshot WHERE AggregateId = @AggregateId", connection);
        command.Parameters.AddWithValue("@AggregateId", loanId);
        return (int?)await command.ExecuteScalarAsync();
    }
}
