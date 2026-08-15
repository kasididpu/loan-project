namespace LoanProject.Application.Reports;

/// <summary>
/// Portfolio-level view for operations/risk, served from the Read DB. The NPL
/// ratio is balance-weighted (the share of money at risk) and expressed as a
/// percentage; OverdueThresholdDays states the classification cut-off used.
/// </summary>
public sealed record PortfolioSummary(
    int TotalLoans,
    IReadOnlyDictionary<string, int> CountByStatus,
    decimal TotalOutstanding,
    decimal NonPerformingOutstanding,
    int NonPerformingLoans,
    decimal NplRatioPercent,
    int OverdueThresholdDays);
