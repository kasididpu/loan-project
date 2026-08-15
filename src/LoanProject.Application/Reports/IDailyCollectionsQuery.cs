namespace LoanProject.Application.Reports;

/// <summary>
/// Daily collections from the Read DB over the last <paramref name="windowDays"/>
/// days ending on <paramref name="nowUtc"/>'s date. "now" is passed in for
/// deterministic, testable windows.
/// </summary>
public interface IDailyCollectionsQuery
{
    Task<IReadOnlyList<DailyCollectionRow>> GetAsync(
        DateTime nowUtc, int windowDays, CancellationToken cancellationToken);
}
