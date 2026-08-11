using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Reference case cross-checked in notes/lab-effective-rate.html:
/// 100,000 at flat 6%/year over 12 months -> A = 8,833.33, final = 8,833.37.
/// </summary>
public class FlatScheduleTests
{
    private static IReadOnlyList<Installment> StandardSchedule() =>
        AmortizationCalculator.BuildFlatSchedule(100_000m, 0.06m, 12);

    [Fact]
    public void BuildFlatSchedule_StandardLoan_HasConstantPaymentAndInterest()
    {
        var rows = StandardSchedule();

        Assert.Equal(12, rows.Count);
        // Every fixed installment: same payment, same interest — the flat signature.
        for (var k = 0; k < rows.Count - 1; k++)
        {
            Assert.Equal(8_833.33m, rows[k].Payment);
            Assert.Equal(500.00m, rows[k].InterestPortion); // 100,000 x 6% / 12
            Assert.Equal(8_333.33m, rows[k].PrincipalPortion);
        }
        Assert.Equal(91_666.67m, rows[0].RemainingBalance);
    }

    [Fact]
    public void BuildFlatSchedule_FinalInstallment_AbsorbsDrift()
    {
        var last = StandardSchedule()[^1];

        Assert.Equal(8_833.37m, last.Payment); // 8,333.37 remainder + 500.00 interest
        Assert.Equal(500.00m, last.InterestPortion);
        Assert.Equal(0m, last.RemainingBalance);
    }

    [Fact]
    public void BuildFlatSchedule_TotalInterest_MatchesFlatFormulaExactly()
    {
        // P x rate x years = 100,000 x 6% x 1 = 6,000 — to the satang.
        Assert.Equal(6_000.00m, StandardSchedule().Sum(r => r.InterestPortion));
    }

    [Fact]
    public void BuildFlatSchedule_PrincipalPortions_SumExactlyToPrincipal()
    {
        Assert.Equal(100_000m, StandardSchedule().Sum(r => r.PrincipalPortion));
    }

    [Fact]
    public void BuildFlatSchedule_EveryRow_PaymentSplitsExactly()
    {
        foreach (var row in StandardSchedule())
            Assert.Equal(row.Payment, row.InterestPortion + row.PrincipalPortion);
    }

    [Fact]
    public void BuildFlatSchedule_ZeroRate_SplitsPrincipalLikeReducing()
    {
        var rows = AmortizationCalculator.BuildFlatSchedule(100m, 0m, 3);

        Assert.Equal(33.33m, rows[0].Payment);
        Assert.Equal(33.33m, rows[1].Payment);
        Assert.Equal(33.34m, rows[2].Payment);
        Assert.All(rows, r => Assert.Equal(0m, r.InterestPortion));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50_000)]
    public void BuildFlatSchedule_WithNonPositivePrincipal_Throws(decimal principal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AmortizationCalculator.BuildFlatSchedule(principal, 0.06m, 12));
    }
}
