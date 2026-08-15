using LoanProject.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Daily collections from the Read DB. Loads the installments touching the
/// window (due or paid inside it, over the indexed date columns) and hands them
/// to <see cref="DailyCollectionsMath"/>, where the day-by-day math is
/// unit-tested without a database.
/// </summary>
public sealed class DailyCollectionsQuery : IDailyCollectionsQuery
{
    private readonly ReadDbContext _db;

    public DailyCollectionsQuery(ReadDbContext db) => _db = db;

    public async Task<IReadOnlyList<DailyCollectionRow>> GetAsync(
        DateTime nowUtc, int windowDays, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(nowUtc.Date);
        var fromUtc = today.AddDays(-(windowDays - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        // Exclusive upper bound: strictly before the start of tomorrow.
        var toUtc = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var installments = await _db.Installments.AsNoTracking()
            .Where(i => (i.DueDateUtc >= fromUtc && i.DueDateUtc < toUtc)
                     || (i.PaidAtUtc >= fromUtc && i.PaidAtUtc < toUtc))
            .ToListAsync(cancellationToken);

        return DailyCollectionsMath.Compute(installments, nowUtc, windowDays);
    }
}
