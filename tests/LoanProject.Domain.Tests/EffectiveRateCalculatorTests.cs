using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Reference figures cross-checked in notes/lab-effective-rate.html:
/// flat 6%/12m payment 8,833.33 -> IRR 0.9080%/month -> 10.90%/year nominal.
/// </summary>
public class EffectiveRateCalculatorTests
{
    [Fact]
    public void MonthlyRate_RoundTripsReducingSchedulePayment()
    {
        // The payment produced BY a 1%/month reducing schedule must recover
        // ~1%/month when fed back through the IRR solver.
        var payment = AmortizationCalculator.BuildSchedule(100_000m, 0.12m, 12)[0].Payment; // 8,884.88

        var rate = EffectiveRateCalculator.MonthlyRateFromAnnuity(100_000m, payment, 12);

        Assert.InRange(rate, 0.0099m, 0.0101m);
    }

    [Fact]
    public void MonthlyRate_OfFlatLoanPayment_MatchesLabReference()
    {
        var rate = EffectiveRateCalculator.MonthlyRateFromAnnuity(100_000m, 8_833.33m, 12);

        Assert.InRange(rate, 0.00907m, 0.00909m); // lab: 0.9080%/month
    }

    [Fact]
    public void AnnualEffectiveFromFlat_SixPercentTwelveMonths_IsNearlyDouble()
    {
        var annual = EffectiveRateCalculator.AnnualEffectiveFromFlat(100_000m, 0.06m, 12);

        Assert.InRange(annual, 0.1085m, 0.1095m); // lab: 10.90% — not 6%!
    }

    [Fact]
    public void AnnualEffectiveFromFlat_FourYearTerm_MatchesLabReference()
    {
        var annual = EffectiveRateCalculator.AnnualEffectiveFromFlat(100_000m, 0.06m, 48);

        Assert.InRange(annual, 0.1092m, 0.1102m); // lab: 10.97%
    }

    [Fact]
    public void ReducingScheduleAtRecoveredRate_ReproducesFlatPayment()
    {
        // The equivalence proof from the lab: a reducing-balance schedule at
        // the recovered IRR must produce (almost) the same fixed payment.
        var monthly = EffectiveRateCalculator.MonthlyRateFromAnnuity(100_000m, 8_833.33m, 12);

        var reproduced = AmortizationCalculator.BuildSchedule(100_000m, monthly * 12m, 12)[0].Payment;

        Assert.InRange(reproduced, 8_833.32m, 8_833.34m);
    }

    [Fact]
    public void MonthlyRate_ZeroInterestCashflow_ReturnsZero()
    {
        // 12 payments of 1,000 repay exactly 12,000: no interest at all.
        Assert.Equal(0m, EffectiveRateCalculator.MonthlyRateFromAnnuity(12_000m, 1_000m, 12));
    }

    [Fact]
    public void MonthlyRate_PaymentRepaysLessThanPrincipal_Throws()
    {
        // Total repaid below the amount borrowed implies a negative rate —
        // outside this domain, so the caller must have made a mistake.
        Assert.Throws<ArgumentException>(
            () => EffectiveRateCalculator.MonthlyRateFromAnnuity(100_000m, 1_000m, 12));
    }

    [Theory]
    [InlineData(0, 8_000, 12)]
    [InlineData(100_000, 0, 12)]
    [InlineData(100_000, 8_000, 0)]
    public void MonthlyRate_WithNonPositiveInputs_Throws(decimal principal, decimal payment, int termMonths)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EffectiveRateCalculator.MonthlyRateFromAnnuity(principal, payment, termMonths));
    }
}
