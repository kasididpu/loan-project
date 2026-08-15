using LoanProject.Application.Reports;
using LoanProject.Domain.Loans;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Pure portfolio math over read-model rows — no database, so every branch is
/// unit-tested directly. NPL follows the Bank of Thailand rule: an active loan
/// more than <c>overdueThresholdDays</c> (90) past its next due date, or one
/// already marked defaulted, is non-performing. The ratio is balance-weighted
/// (industry standard) — the share of money at risk, not the share of contracts:
///     NPL% = Σ outstanding(non-performing) / Σ outstanding(active + defaulted)
/// </summary>
public static class PortfolioMath
{
    public static PortfolioSummary Compute(
        IReadOnlyCollection<LoanReadModel> loans, DateTime nowUtc, int overdueThresholdDays)
    {
        var countByStatus = loans
            .GroupBy(l => l.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        // "Money out" = active or defaulted loans; other statuses carry no
        // outstanding balance to weigh.
        var exposed = loans
            .Where(l => l.Status is nameof(LoanStatus.Active) or nameof(LoanStatus.Defaulted))
            .ToList();
        var totalOutstanding = exposed.Sum(l => l.OutstandingBalance);

        var nonPerforming = exposed.Where(l => IsNonPerforming(l, nowUtc, overdueThresholdDays)).ToList();
        var nonPerformingOutstanding = nonPerforming.Sum(l => l.OutstandingBalance);

        // Balance-weighted ratio as a percentage. Explicit AwayFromZero, the
        // same rounding rule as every money figure in the system; the empty
        // book reads 0 rather than dividing by zero.
        var nplRatioPercent = totalOutstanding == 0m
            ? 0m
            : Math.Round(nonPerformingOutstanding / totalOutstanding * 100m, 2, MidpointRounding.AwayFromZero);

        return new PortfolioSummary(
            loans.Count,
            countByStatus,
            totalOutstanding,
            nonPerformingOutstanding,
            nonPerforming.Count,
            nplRatioPercent,
            overdueThresholdDays);
    }

    public static bool IsNonPerforming(LoanReadModel loan, DateTime nowUtc, int overdueThresholdDays)
    {
        if (loan.Status == nameof(LoanStatus.Defaulted))
            return true;

        if (loan.Status != nameof(LoanStatus.Active) || loan.NextDueDateUtc is null)
            return false;

        // Strictly greater than the threshold: 90 days is still "current", 91 is not.
        var daysOverdue = (nowUtc.Date - loan.NextDueDateUtc.Value.Date).Days;
        return daysOverdue > overdueThresholdDays;
    }
}
