using LoanProject.Domain.Loans;

namespace LoanProject.Domain.Tests;

/// <summary>
/// Reference case cross-checked by hand and in notes/lab-amortization.html:
/// 100,000 at 12%/year over 12 months -> A = 8,884.88, final = 8,884.85.
/// </summary>
public class AmortizationCalculatorTests
{
    private static IReadOnlyList<Installment> StandardSchedule() =>
        AmortizationCalculator.BuildSchedule(100_000m, 0.12m, 12);

    [Fact]
    public void BuildSchedule_StandardLoan_HasOneRowPerMonth()
    {
        Assert.Equal(12, StandardSchedule().Count);
    }

    [Fact]
    public void BuildSchedule_StandardLoan_ProducesKnownFirstRows()
    {
        var rows = StandardSchedule();

        Assert.Equal(8_884.88m, rows[0].Payment);
        Assert.Equal(1_000.00m, rows[0].InterestPortion);   // 100,000 x 1%
        Assert.Equal(7_884.88m, rows[0].PrincipalPortion);
        Assert.Equal(92_115.12m, rows[0].RemainingBalance);

        Assert.Equal(921.15m, rows[1].InterestPortion);      // 92,115.12 x 1%
    }

    [Fact]
    public void BuildSchedule_FinalInstallment_AbsorbsRoundingDrift()
    {
        var rows = StandardSchedule();
        var last = rows[^1];

        Assert.Equal(8_884.85m, last.Payment); // != A, by design
        Assert.Equal(0m, last.RemainingBalance);
        Assert.Equal(last.InterestPortion + last.PrincipalPortion, last.Payment);
    }

    [Fact]
    public void BuildSchedule_PrincipalPortions_SumExactlyToPrincipal()
    {
        var rows = StandardSchedule();

        Assert.Equal(100_000m, rows.Sum(r => r.PrincipalPortion));
    }

    [Fact]
    public void BuildSchedule_EveryRow_PaymentSplitsExactlyIntoInterestPlusPrincipal()
    {
        foreach (var row in StandardSchedule())
            Assert.Equal(row.Payment, row.InterestPortion + row.PrincipalPortion);
    }

    [Fact]
    public void BuildSchedule_InterestFallsAndPrincipalRises_AcrossFixedInstallments()
    {
        var rows = StandardSchedule();

        // The final row is excluded: it pays the exact remainder, not A.
        for (var k = 1; k < rows.Count - 1; k++)
        {
            Assert.True(rows[k].InterestPortion < rows[k - 1].InterestPortion);
            Assert.True(rows[k].PrincipalPortion > rows[k - 1].PrincipalPortion);
        }
    }

    [Fact]
    public void BuildSchedule_ZeroRate_SplitsPrincipalEvenly()
    {
        var rows = AmortizationCalculator.BuildSchedule(120_000m, 0m, 12);

        Assert.All(rows, r => Assert.Equal(0m, r.InterestPortion));
        Assert.All(rows, r => Assert.Equal(10_000.00m, r.Payment));
        Assert.Equal(0m, rows[^1].RemainingBalance);
    }

    [Fact]
    public void BuildSchedule_ZeroRate_RemainderLandsInFinalInstallment()
    {
        // 100 / 3 does not divide evenly: 33.33 + 33.33 + 33.34.
        var rows = AmortizationCalculator.BuildSchedule(100m, 0m, 3);

        Assert.Equal(33.33m, rows[0].Payment);
        Assert.Equal(33.33m, rows[1].Payment);
        Assert.Equal(33.34m, rows[2].Payment);
        Assert.Equal(100m, rows.Sum(r => r.PrincipalPortion));
    }

    [Fact]
    public void BuildSchedule_SingleInstallment_PaysPrincipalPlusOneMonthInterest()
    {
        var rows = AmortizationCalculator.BuildSchedule(100_000m, 0.12m, 1);

        var only = Assert.Single(rows);
        Assert.Equal(1_000.00m, only.InterestPortion);
        Assert.Equal(101_000.00m, only.Payment);
        Assert.Equal(0m, only.RemainingBalance);
    }

    [Fact]
    public void BuildSchedule_LongTerm_HoldsAllInvariants()
    {
        // Cross-checked in the lab: 100,000 at 24%/year over 60 months.
        var rows = AmortizationCalculator.BuildSchedule(100_000m, 0.24m, 60);

        Assert.Equal(60, rows.Count);
        Assert.Equal(2_876.80m, rows[0].Payment);
        Assert.Equal(2_876.41m, rows[^1].Payment);
        Assert.Equal(0m, rows[^1].RemainingBalance);
        Assert.Equal(100_000m, rows.Sum(r => r.PrincipalPortion));
        Assert.All(rows, r => Assert.True(r.RemainingBalance >= 0m));
    }

    // --- argument guards ---

    [Theory]
    [InlineData(0)]
    [InlineData(-50_000)]
    public void BuildSchedule_WithNonPositivePrincipal_Throws(decimal principal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AmortizationCalculator.BuildSchedule(principal, 0.12m, 12));
    }

    [Fact]
    public void BuildSchedule_WithNegativeRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AmortizationCalculator.BuildSchedule(100_000m, -0.01m, 12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    public void BuildSchedule_WithNonPositiveTerm_Throws(int termMonths)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AmortizationCalculator.BuildSchedule(100_000m, 0.12m, termMonths));
    }

    [Fact]
    public void BuildSchedule_WithSubSatangPrincipal_Throws()
    {
        // Money enters the system satang-precise; anything finer is a caller bug.
        Assert.Throws<ArgumentException>(
            () => AmortizationCalculator.BuildSchedule(100.005m, 0.12m, 12));
    }
}
