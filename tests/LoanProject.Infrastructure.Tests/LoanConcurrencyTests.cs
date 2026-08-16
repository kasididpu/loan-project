using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The racing-writers proof (roadmap v5 acceptance): two writers load the
/// same version and both append — the second must be rejected by
/// UQ_EventStore_AggVer, and recovery is reload-and-retry: throw the stale
/// instance away, load fresh state, decide again.
/// </summary>
public class LoanConcurrencyTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 14, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static LoanEventStoreRepository CreateRepository() => new(TestDatabase.ConnectionString);

    /// <summary>Persists a fresh active loan and returns its id (stream at version 3).</summary>
    private static async Task<Guid> PersistActiveLoanAsync(LoanEventStoreRepository repository)
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        loan.Approve(Guid.NewGuid(), "officer-1", Now);
        loan.Disburse(Principal, Guid.NewGuid(), "officer", Now);
        await repository.SaveAsync(loan, CancellationToken.None);
        return loan.Id;
    }

    [Fact]
    public async Task SaveAsync_TwoWritersOnSameVersion_SecondThrowsConcurrency()
    {
        var repository = CreateRepository();
        var loanId = await PersistActiveLoanAsync(repository);

        // Both writers load the same version 3 — neither knows about the other.
        var writerA = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        var writerB = (await repository.LoadAsync(loanId, CancellationToken.None))!;

        writerA.ReceivePayment(Guid.NewGuid(), writerA.Schedule![0].Payment, 1, "evt_race_a", Now);
        await repository.SaveAsync(writerA, CancellationToken.None); // wins version 4

        writerB.MarkDefaulted(daysOverdue: 91, Now); // also targets version 4
        var conflict = await Assert.ThrowsAsync<LoanConcurrencyException>(
            () => repository.SaveAsync(writerB, CancellationToken.None));

        Assert.Equal(loanId, conflict.LoanId);
        Assert.Equal(3, conflict.ExpectedVersion);
    }

    [Fact]
    public async Task SaveAsync_AfterConflict_ReloadAndRetrySucceeds()
    {
        var repository = CreateRepository();
        var loanId = await PersistActiveLoanAsync(repository);
        var writerA = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        var writerB = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        writerA.ReceivePayment(Guid.NewGuid(), writerA.Schedule![0].Payment, 1, "evt_race_a2", Now);
        await repository.SaveAsync(writerA, CancellationToken.None);
        writerB.MarkDefaulted(daysOverdue: 91, Now);
        await Assert.ThrowsAsync<LoanConcurrencyException>(
            () => repository.SaveAsync(writerB, CancellationToken.None));

        // Recovery protocol: discard the stale instance, load fresh state
        // (now version 4, payment visible), decide again, append on top.
        var fresh = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        fresh.MarkDefaulted(daysOverdue: 91, Now);
        await repository.SaveAsync(fresh, CancellationToken.None);

        var final = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        Assert.Equal(LoanStatus.Defaulted, final.Status);
        Assert.Equal(5, final.Version);
        Assert.Equal(2, final.NextInstallmentNo); // writer A's payment survived intact
    }

    [Fact]
    public async Task Retry_MustReDecideOnFreshState_BlindResubmitIsRejectedByDomain()
    {
        var repository = CreateRepository();
        var loanId = await PersistActiveLoanAsync(repository);
        var writerA = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        var writerB = (await repository.LoadAsync(loanId, CancellationToken.None))!;

        // Both race to collect installment 1; A wins.
        writerA.ReceivePayment(Guid.NewGuid(), writerA.Schedule![0].Payment, 1, "evt_race_a3", Now);
        await repository.SaveAsync(writerA, CancellationToken.None);
        writerB.ReceivePayment(Guid.NewGuid(), writerB.Schedule![0].Payment, 1, "evt_race_b3", Now);
        await Assert.ThrowsAsync<LoanConcurrencyException>(
            () => repository.SaveAsync(writerB, CancellationToken.None));

        // Retry does NOT mean replaying the same command: on fresh state the
        // state machine itself refuses to collect installment 1 twice.
        var fresh = (await repository.LoadAsync(loanId, CancellationToken.None))!;
        Assert.Throws<ArgumentOutOfRangeException>(
            () => fresh.ReceivePayment(Guid.NewGuid(), fresh.Schedule![0].Payment, 1, "evt_race_b3", Now));
    }
}
