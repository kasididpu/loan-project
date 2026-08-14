namespace LoanProject.Application.Reports;

/// <summary>
/// End-of-day collections per loan. Backed by a stored procedure on
/// purpose: set-based aggregation belongs next to the data — only the
/// summary crosses the wire, never the raw rows.
/// </summary>
public interface IEndOfDaySummaryQuery
{
    Task<IReadOnlyList<EndOfDayLoanSummary>> GetAsync(DateOnly asOfDate, CancellationToken cancellationToken);
}
