namespace LoanProject.Application.Reports;

/// <summary>
/// One day's due-vs-collected figures for the daily collections report.
/// CollectionRatePercent is null when nothing fell due that day (no denominator).
/// </summary>
public sealed record DailyCollectionRow(
    DateOnly Date,
    decimal Due,
    decimal Collected,
    decimal? CollectionRatePercent);
