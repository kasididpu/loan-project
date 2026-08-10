using LoanProject.Domain.Loans;
using LoanProject.Domain.Loans.Events;

namespace LoanProject.Domain.Tests;

/// <summary>Every VALID transition of the Loan state machine, plus replay.</summary>
public class LoanLifecycleTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static Loan OriginatedLoan() =>
        Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, annualRate: 0.12m, RateType.Effective, termMonths: 12, Now);

    private static Loan ActiveLoan()
    {
        var loan = OriginatedLoan();
        loan.Approve("officer-1", Now);
        loan.Disburse(Principal, Now);
        return loan;
    }

    [Fact]
    public void Originate_WithValidData_StartsInOriginatedStatusAtVersion1()
    {
        var loan = OriginatedLoan();

        Assert.Equal(LoanStatus.Originated, loan.Status);
        Assert.Equal(1, loan.Version);
        Assert.Equal(0m, loan.OutstandingBalance); // no debt until money is disbursed
        var evt = Assert.Single(loan.UncommittedEvents);
        Assert.IsType<LoanOriginated>(evt);
    }

    [Fact]
    public void Approve_WhenOriginated_MovesToApproved()
    {
        var loan = OriginatedLoan();

        loan.Approve("officer-1", Now);

        Assert.Equal(LoanStatus.Approved, loan.Status);
    }

    [Fact]
    public void Reject_WhenOriginated_MovesToRejected()
    {
        var loan = OriginatedLoan();

        loan.Reject("officer-1", "insufficient income", Now);

        Assert.Equal(LoanStatus.Rejected, loan.Status);
    }

    [Fact]
    public void Disburse_WhenApproved_ActivatesLoanAndStartsOutstandingBalance()
    {
        var loan = OriginatedLoan();
        loan.Approve("officer-1", Now);

        loan.Disburse(Principal, Now);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(Principal, loan.OutstandingBalance);
    }

    [Fact]
    public void ReceivePayment_WhenActive_ReducesOutstandingBalance()
    {
        var loan = ActiveLoan();

        loan.ReceivePayment(Guid.NewGuid(), 40_000m, installmentNo: 1, "evt_test_1", Now);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(60_000m, loan.OutstandingBalance);
    }

    [Fact]
    public void Settle_WhenOutstandingIsZero_MovesToSettled()
    {
        var loan = ActiveLoan();
        var finalPaymentId = Guid.NewGuid();
        loan.ReceivePayment(finalPaymentId, Principal, installmentNo: 1, "evt_test_1", Now);

        loan.Settle(finalPaymentId, Now);

        Assert.Equal(LoanStatus.Settled, loan.Status);
        Assert.Equal(0m, loan.OutstandingBalance);
    }

    [Fact]
    public void MarkDefaulted_WhenActive_MovesToDefaulted()
    {
        var loan = ActiveLoan();

        loan.MarkDefaulted(daysOverdue: 91, Now);

        Assert.Equal(LoanStatus.Defaulted, loan.Status);
    }

    [Fact]
    public void Version_IncrementsOncePerEvent()
    {
        var loan = ActiveLoan(); // originate + approve + disburse = 3 events

        Assert.Equal(3, loan.Version);
        Assert.Equal(3, loan.UncommittedEvents.Count);
    }

    [Fact]
    public void LoadFromHistory_ReplaysToIdenticalState()
    {
        var original = ActiveLoan();
        original.ReceivePayment(Guid.NewGuid(), 25_000m, installmentNo: 1, "evt_test_1", Now);

        var replayed = Loan.LoadFromHistory(original.UncommittedEvents);

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.Status, replayed.Status);
        Assert.Equal(original.OutstandingBalance, replayed.OutstandingBalance);
        Assert.Equal(original.Version, replayed.Version);
        // Replayed events are history, not new facts — nothing to persist again.
        Assert.Empty(replayed.UncommittedEvents);
    }
}
