using LoanProject.Infrastructure.ReadModel;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Pure NPL math — no database. The rule under test: balance-weighted ratio of
/// active-overdue-past-90-days plus already-defaulted loans, over all money out.
/// </summary>
public class PortfolioMathTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
    private const int Threshold = 90;

    private static LoanReadModel Loan(string status, decimal outstanding, DateTime? nextDue) => new()
    {
        LoanId = Guid.NewGuid(),
        Status = status,
        OutstandingBalance = outstanding,
        NextDueDateUtc = nextDue,
    };

    [Fact]
    public void Compute_EmptyBook_RatioZero()
    {
        var summary = PortfolioMath.Compute(Array.Empty<LoanReadModel>(), Now, Threshold);

        Assert.Equal(0, summary.TotalLoans);
        Assert.Equal(0m, summary.TotalOutstanding);
        Assert.Equal(0m, summary.NplRatioPercent);
    }

    [Fact]
    public void Compute_ActiveCurrent_NotNonPerforming()
    {
        var loans = new[] { Loan("Active", 50_000m, Now) };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(0, summary.NonPerformingLoans);
        Assert.Equal(0m, summary.NplRatioPercent);
        Assert.Equal(50_000m, summary.TotalOutstanding);
    }

    [Fact]
    public void Compute_ActiveExactlyThresholdDaysOverdue_NotNonPerforming()
    {
        // 90 days is still "current"; the cut-off is strictly greater than 90.
        var loans = new[] { Loan("Active", 50_000m, Now.AddDays(-90)) };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(0, summary.NonPerformingLoans);
        Assert.Equal(0m, summary.NplRatioPercent);
    }

    [Fact]
    public void Compute_ActivePastThreshold_IsNonPerforming()
    {
        var loans = new[] { Loan("Active", 50_000m, Now.AddDays(-91)) };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(1, summary.NonPerformingLoans);
        Assert.Equal(100.00m, summary.NplRatioPercent);
    }

    [Fact]
    public void Compute_Defaulted_IsNonPerformingRegardlessOfDate()
    {
        var loans = new[] { Loan("Defaulted", 40_000m, null) };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(1, summary.NonPerformingLoans);
        Assert.Equal(40_000m, summary.NonPerformingOutstanding);
        Assert.Equal(100.00m, summary.NplRatioPercent);
    }

    [Fact]
    public void Compute_RatioIsBalanceWeighted()
    {
        var loans = new[]
        {
            Loan("Active", 70_000m, Now),                 // current -> performing
            Loan("Active", 30_000m, Now.AddDays(-120)),   // overdue -> non-performing
        };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(100_000m, summary.TotalOutstanding);
        Assert.Equal(30_000m, summary.NonPerformingOutstanding);
        Assert.Equal(30.00m, summary.NplRatioPercent); // by balance, not 50% by count
    }

    [Fact]
    public void Compute_RoundsRatioAwayFromZero()
    {
        var loans = new[]
        {
            Loan("Active", 200_000m, Now),                // performing
            Loan("Active", 100_000m, Now.AddDays(-120)),  // non-performing
        };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        // 100000 / 300000 = 33.3333... -> 33.33
        Assert.Equal(33.33m, summary.NplRatioPercent);
    }

    [Fact]
    public void Compute_IgnoresNonExposedStatusesInOutstanding()
    {
        var loans = new[]
        {
            Loan("Originated", 0m, null),
            Loan("Approved", 0m, null),
            Loan("Rejected", 0m, null),
            Loan("Settled", 0m, null),
            Loan("Active", 10_000m, Now),
        };

        var summary = PortfolioMath.Compute(loans, Now, Threshold);

        Assert.Equal(5, summary.TotalLoans);
        Assert.Equal(10_000m, summary.TotalOutstanding); // only the active loan is "money out"
        Assert.Equal(0m, summary.NplRatioPercent);
    }
}
