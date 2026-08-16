using LoanProject.Domain.Loans;
using LoanProject.Domain.Loans.Events;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Phase 8: lifecycle events carry the acting officer's immutable id (the token
/// subject) alongside the display name, so the append-only ledger is a
/// trustworthy audit trail. The id is a required part of each command.
/// </summary>
public class LoanActorAuditTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    private static Loan NewOriginatedLoan() =>
        Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), 100_000m, 0.12m, RateType.Effective, 12, Now);

    [Fact]
    public void Approve_CapturesActorIdAndNameOnEvent()
    {
        var officerId = Guid.NewGuid();
        var loan = NewOriginatedLoan();

        loan.Approve(officerId, "officer", Now);

        var approved = Assert.IsType<LoanApproved>(loan.UncommittedEvents[^1]);
        Assert.Equal(officerId, approved.ApprovedByUserId);
        Assert.Equal("officer", approved.ApprovedBy);
    }

    [Fact]
    public void Disburse_CapturesActorIdOnEvent()
    {
        var officerId = Guid.NewGuid();
        var loan = NewOriginatedLoan();
        loan.Approve(officerId, "officer", Now);

        loan.Disburse(100_000m, officerId, "officer", Now);

        var disbursed = Assert.IsType<LoanDisbursed>(loan.UncommittedEvents[^1]);
        Assert.Equal(officerId, disbursed.DisbursedByUserId);
    }

    [Fact]
    public void Reject_CapturesActorIdOnEvent()
    {
        var officerId = Guid.NewGuid();
        var loan = NewOriginatedLoan();

        loan.Reject(officerId, "officer", "insufficient income", Now);

        var rejected = Assert.IsType<LoanRejected>(loan.UncommittedEvents[^1]);
        Assert.Equal(officerId, rejected.RejectedByUserId);
    }
}
