using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Reference case cross-checked in notes/lab-late-fee.html:
/// overdue principal 7,884.88 at 15%/year, 30 days late -> 97.21.
/// </summary>
public class LateFeeCalculatorTests
{
    [Fact]
    public void Calculate_KnownExample_MatchesLabReference()
    {
        var fee = LateFeeCalculator.Calculate(7_884.88m, annualPenaltyRate: 0.15m, daysLate: 30);

        Assert.Equal(97.21m, fee);
    }

    [Fact]
    public void Calculate_ZeroDaysLate_IsZero()
    {
        Assert.Equal(0m, LateFeeCalculator.Calculate(7_884.88m, 0.15m, daysLate: 0));
    }

    [Fact]
    public void Calculate_ZeroPenaltyRate_IsZero()
    {
        Assert.Equal(0m, LateFeeCalculator.Calculate(7_884.88m, 0m, daysLate: 30));
    }

    [Fact]
    public void Calculate_RoundsToSatang()
    {
        // 1,000 x 15% x 1/365 = 0.41095... -> 0.41
        Assert.Equal(0.41m, LateFeeCalculator.Calculate(1_000m, 0.15m, daysLate: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5_000)]
    public void Calculate_WithNonPositiveOverduePrincipal_Throws(decimal overduePrincipal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LateFeeCalculator.Calculate(overduePrincipal, 0.15m, 30));
    }

    [Fact]
    public void Calculate_WithNegativePenaltyRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LateFeeCalculator.Calculate(7_884.88m, -0.01m, 30));
    }

    [Fact]
    public void Calculate_WithNegativeDaysLate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LateFeeCalculator.Calculate(7_884.88m, 0.15m, -1));
    }
}
