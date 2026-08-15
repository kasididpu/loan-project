using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// The snapshot memento: every non-derived field survives the round trip,
/// the schedule is rebuilt (never stored), and replay can continue from
/// the snapshot with only the tail events.
/// </summary>
public class LoanSnapshotTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static Loan ActiveLoan()
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);
        loan.Disburse(Principal, Now);
        return loan;
    }

    [Fact]
    public void ToSnapshot_ActiveLoan_CapturesAllPersistentState()
    {
        var loan = ActiveLoan();

        var snapshot = loan.ToSnapshot();

        Assert.Equal(loan.Id, snapshot.Id);
        Assert.Equal(loan.CustomerId, snapshot.CustomerId);
        Assert.Equal(loan.Status, snapshot.Status);
        Assert.Equal(loan.Principal, snapshot.Principal);
        Assert.Equal(loan.AnnualRate, snapshot.AnnualRate);
        Assert.Equal(loan.RateType, snapshot.RateType);
        Assert.Equal(loan.TermMonths, snapshot.TermMonths);
        Assert.Equal(loan.OutstandingBalance, snapshot.OutstandingBalance);
        Assert.Equal(loan.NextInstallmentNo, snapshot.NextInstallmentNo);
        Assert.Equal(loan.Version, snapshot.Version);
    }

    [Fact]
    public void FromSnapshot_NoTail_RebuildsIdenticalStateIncludingSchedule()
    {
        var original = ActiveLoan();
        original.ReceivePayment(Guid.NewGuid(), original.Schedule![0].Payment, 1, "evt_test_1", Now);

        var restored = Loan.FromSnapshot(original.ToSnapshot(), []);

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.CustomerId, restored.CustomerId);
        Assert.Equal(original.Status, restored.Status);
        Assert.Equal(original.OutstandingBalance, restored.OutstandingBalance);
        Assert.Equal(original.NextInstallmentNo, restored.NextInstallmentNo);
        Assert.Equal(original.Version, restored.Version);
        // Not stored anywhere in the snapshot — must be rebuilt by the calculators.
        Assert.Equal(original.Schedule, restored.Schedule);
        // A restored loan is history, not new facts.
        Assert.Empty(restored.UncommittedEvents);
    }

    [Fact]
    public void FromSnapshot_WithSubsequentEvents_ContinuesReplay()
    {
        var loan = ActiveLoan();
        var snapshot = loan.ToSnapshot(); // taken at version 3 (active, nothing paid)
        loan.ReceivePayment(Guid.NewGuid(), loan.Schedule![0].Payment, 1, "evt_test_1", Now);
        loan.ReceivePayment(Guid.NewGuid(), loan.Schedule![1].Payment, 2, "evt_test_2", Now);
        var tail = loan.UncommittedEvents.Skip(3); // only the two payments after the snapshot

        var restored = Loan.FromSnapshot(snapshot, tail);

        Assert.Equal(loan.OutstandingBalance, restored.OutstandingBalance);
        Assert.Equal(3, restored.NextInstallmentNo);
        Assert.Equal(loan.Version, restored.Version);
    }

    [Fact]
    public void FromSnapshot_BeforeDisbursement_HasNoSchedule()
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);

        var restored = Loan.FromSnapshot(loan.ToSnapshot(), []);

        Assert.Equal(LoanStatus.Originated, restored.Status);
        Assert.Null(restored.Schedule);
    }
}
