namespace LoanProject.Application.Reports;

/// <summary>
/// Portfolio summary from the Read DB. "now" is passed in (not read from the
/// clock inside) so the overdue/NPL calculation is deterministic and testable.
/// </summary>
public interface IPortfolioSummaryQuery
{
    Task<PortfolioSummary> GetAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
