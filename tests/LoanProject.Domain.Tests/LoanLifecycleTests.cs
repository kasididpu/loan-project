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
    public void Disburse_WhenApproved_ActivatesLoanAndBuildsSchedule()
    {
        var loan = OriginatedLoan();
        loan.Approve("officer-1", Now);

        loan.Disburse(Principal, Now);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(Principal, loan.OutstandingBalance);
        Assert.NotNull(loan.Schedule);
        Assert.Equal(12, loan.Schedule!.Count);
    }

    [Fact]
    public void ReceivePayment_ExactDueAmount_AdvancesBalanceToScheduleRow()
    {
        var loan = ActiveLoan();

        loan.ReceivePayment(Guid.NewGuid(), 8_884.88m, installmentNo: 1, "evt_test_1", Now);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(92_115.12m, loan.OutstandingBalance); // remaining balance of schedule row 1
        Assert.Equal(2, loan.NextInstallmentNo);
    }

    [Fact]
    public void Settle_AfterAllInstallmentsPaid_MovesToSettled()
    {
        var loan = ActiveLoan();
        var lastPaymentId = Guid.Empty;
        foreach (var row in loan.Schedule!)
        {
            lastPaymentId = Guid.NewGuid();
            loan.ReceivePayment(lastPaymentId, row.Payment, row.Number, $"evt_test_{row.Number}", Now);
        }

        loan.Settle(lastPaymentId, Now);

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
        original.ReceivePayment(Guid.NewGuid(), 8_884.88m, 1, "evt_test_1", Now);

        var replayed = Loan.LoadFromHistory(original.UncommittedEvents);

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.Status, replayed.Status);
        Assert.Equal(original.OutstandingBalance, replayed.OutstandingBalance);
        Assert.Equal(original.Version, replayed.Version);
        Assert.Equal(original.NextInstallmentNo, replayed.NextInstallmentNo);
        // Installment is a record: Assert.Equal compares the rows by value.
        Assert.Equal(original.Schedule, replayed.Schedule);
        // Replayed events are history, not new facts — nothing to persist again.
        Assert.Empty(replayed.UncommittedEvents);
    }
}
