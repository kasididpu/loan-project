using LoanProject.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Portfolio summary from the Read DB. Loads the (showcase-sized) book and hands
/// it to <see cref="PortfolioMath"/> for the balance-weighted NPL calculation —
/// the money logic lives there, unit-tested without a database. At real volume
/// the aggregation would move into SQL or be precomputed by a job.
/// </summary>
public sealed class PortfolioSummaryQuery : IPortfolioSummaryQuery
{
    /// <summary>BOT NPL classification: overdue beyond three months.</summary>
    public const int OverdueThresholdDays = 90;

    private readonly ReadDbContext _db;

    public PortfolioSummaryQuery(ReadDbContext db) => _db = db;

    public async Task<PortfolioSummary> GetAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var loans = await _db.Loans.AsNoTracking().ToListAsync(cancellationToken);
        return PortfolioMath.Compute(loans, nowUtc, OverdueThresholdDays);
    }
}
