using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// The amortization schedule wired into the aggregate, and the exact-amount
/// payment policy: pay the due installment's exact figure, in order —
/// under, over, and out-of-order are all refused.
/// </summary>
public class LoanScheduleTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static Loan EffectiveLoan()
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);
        loan.Disburse(Principal, Now);
        return loan;
    }

    [Fact]
    public void Disburse_EffectiveLoan_BuildsReducingSchedule()
    {
        var loan = EffectiveLoan();

        // Derived state must be exactly what the calculator produces.
        Assert.Equal(AmortizationCalculator.BuildSchedule(Principal, 0.12m, 12), loan.Schedule);
    }

    [Fact]
    public void Disburse_FlatLoan_BuildsFlatSchedule()
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.06m, RateType.Flat, 12, Now);
        loan.Approve("officer-1", Now);

        loan.Disburse(Principal, Now);

        Assert.Equal(AmortizationCalculator.BuildFlatSchedule(Principal, 0.06m, 12), loan.Schedule);
        Assert.Equal(8_833.33m, loan.Schedule![0].Payment);
    }

    [Fact]
    public void Schedule_IsNullBeforeDisbursement()
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);

        Assert.Null(loan.Schedule);
    }

    [Fact]
    public void ReceivePayment_Underpayment_Throws()
    {
        var loan = EffectiveLoan();

        Assert.Throws<ArgumentException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 8_884.87m, 1, "evt_test_1", Now));
    }

    [Fact]
    public void ReceivePayment_Overpayment_Throws()
    {
        var loan = EffectiveLoan();

        Assert.Throws<ArgumentException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 8_884.89m, 1, "evt_test_1", Now));
    }

    [Fact]
    public void ReceivePayment_OutOfOrderInstallment_Throws()
    {
        var loan = EffectiveLoan();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 8_884.88m, installmentNo: 2, "evt_test_2", Now));
    }

    [Fact]
    public void FinalInstallment_PayingRegularAmount_Throws()
    {
        // The final installment absorbed the rounding drift: it costs
        // 8,884.85, so paying the regular 8,884.88 would overpay 3 satang.
        var loan = PayUntilFinal(out _);

        Assert.Throws<ArgumentException>(
            () => loan.ReceivePayment(Guid.NewGuid(), 8_884.88m, 12, "evt_test_12", Now));
    }

    [Fact]
    public void FinalInstallment_ExactAmount_EnablesSettle()
    {
        var loan = PayUntilFinal(out var finalAmount);
        var finalPaymentId = Guid.NewGuid();

        loan.ReceivePayment(finalPaymentId, finalAmount, 12, "evt_test_12", Now);
        loan.Settle(finalPaymentId, Now);

        Assert.Equal(LoanStatus.Settled, loan.Status);
        Assert.Equal(0m, loan.OutstandingBalance);
    }

    [Fact]
    public void Replay_RebuildsScheduleAndPaymentProgress()
    {
        var original = EffectiveLoan();
        original.ReceivePayment(Guid.NewGuid(), original.Schedule![0].Payment, 1, "evt_test_1", Now);
        original.ReceivePayment(Guid.NewGuid(), original.Schedule![1].Payment, 2, "evt_test_2", Now);

        var replayed = Loan.LoadFromHistory(original.UncommittedEvents);

        Assert.Equal(original.Schedule, replayed.Schedule);
        Assert.Equal(original.OutstandingBalance, replayed.OutstandingBalance);
        Assert.Equal(3, replayed.NextInstallmentNo);
    }

    private static Loan PayUntilFinal(out decimal finalAmount)
    {
        var loan = EffectiveLoan();
        foreach (var row in loan.Schedule!.Take(11))
            loan.ReceivePayment(Guid.NewGuid(), row.Payment, row.Number, $"evt_test_{row.Number}", Now);
        finalAmount = loan.Schedule![11].Payment; // 8,884.85
        return loan;
    }
}
