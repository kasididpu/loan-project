using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Every INVALID transition and argument guard. Project rule: each throw
/// site in the aggregate must appear here.
/// </summary>
public class LoanGuardTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static Loan OriginatedLoan() =>
        Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, annualRate: 0.12m, RateType.Effective, termMonths: 12, Now);

    private static Loan ApprovedLoan()
    {
        var loan = OriginatedLoan();
        loan.Approve(Guid.NewGuid(), "officer-1", Now);
        return loan;
    }

    private static Loan ActiveLoan()
    {
        var loan = ApprovedLoan();
        loan.Disburse(Principal, Guid.NewGuid(), "officer", Now);
        return loan;
    }

    private static Loan SettledLoan()
    {
        var loan = ActiveLoan();
        var lastPaymentId = Guid.Empty;
        foreach (var row in loan.Schedule!)
        {
            lastPaymentId = Guid.NewGuid();
            loan.ReceivePayment(lastPaymentId, row.Payment, row.Number, $"evt_test_{row.Number}", Now);
        }
        loan.Settle(lastPaymentId, Now);
        return loan;
    }

    private static Loan RejectedLoan()
    {
        var loan = OriginatedLoan();
        loan.Reject(Guid.NewGuid(), "officer-1", "insufficient income", Now);
        return loan;
    }

    // --- invalid transitions ---

    [Fact]
    public void Approve_WhenAlreadyApproved_Throws()
    {
        var loan = ApprovedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Approve(Guid.NewGuid(), "officer-2", Now));
    }

    [Fact]
    public void Reject_WhenApproved_Throws()
    {
        var loan = ApprovedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Reject(Guid.NewGuid(), "officer-2", "changed mind", Now));
    }

    [Fact]
    public void Disburse_WhenNotApproved_Throws()
    {
        var loan = OriginatedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Disburse(Principal, Guid.NewGuid(), "officer", Now));
    }

    [Fact]
    public void ReceivePayment_WhenNotActive_Throws()
    {
        var loan = ApprovedLoan(); // approved but money not yet disbursed

        Assert.Throws<InvalidLoanTransitionException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 1_000m, 1, "evt_test_1", Now));
    }

    [Fact]
    public void Settle_WhenOutstandingRemains_Throws()
    {
        var loan = ActiveLoan();
        loan.ReceivePayment(Guid.NewGuid(), 8_884.88m, 1, "evt_test_1", Now); // exact installment 1

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Settle(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Settle_WhenNotActive_Throws()
    {
        var loan = ApprovedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Settle(Guid.NewGuid(), Now));
    }

    [Fact]
    public void MarkDefaulted_WhenNotActive_Throws()
    {
        var loan = OriginatedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.MarkDefaulted(91, Now));
    }

    [Fact]
    public void AnyCommand_WhenRejected_Throws()
    {
        var loan = RejectedLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Approve(Guid.NewGuid(), "officer-1", Now));
        Assert.Throws<InvalidLoanTransitionException>(() => loan.Disburse(Principal, Guid.NewGuid(), "officer", Now));
        Assert.Throws<InvalidLoanTransitionException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 1_000m, 1, "evt_test_1", Now));
    }

    [Fact]
    public void AnyCommand_WhenSettled_Throws()
    {
        var loan = SettledLoan();

        Assert.Throws<InvalidLoanTransitionException>(() => loan.Approve(Guid.NewGuid(), "officer-1", Now));
        Assert.Throws<InvalidLoanTransitionException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 1_000m, 1, "evt_test_1", Now));
        Assert.Throws<InvalidLoanTransitionException>(() => loan.MarkDefaulted(91, Now));
    }

    // --- argument guards ---

    [Theory]
    [InlineData(0)]
    [InlineData(-5_000)]
    public void Originate_WithNonPositivePrincipal_Throws(decimal principal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), principal, 0.12m, RateType.Effective, 12, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void ReceivePayment_WithNonPositiveAmount_Throws(decimal amount)
    {
        var loan = ActiveLoan();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => loan.ReceivePayment(Guid.NewGuid(), amount, 1, "evt_test_1", Now));
    }

    [Fact]
    public void Disburse_AmountDifferentFromPrincipal_Throws()
    {
        var loan = ApprovedLoan();

        Assert.Throws<ArgumentOutOfRangeException>(() => loan.Disburse(Principal - 1m, Guid.NewGuid(), "officer", Now));
    }
}
