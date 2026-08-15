using System.Globalization;
using LoanProject.Application.Audit;
using LoanProject.Application.Reports;

namespace LoanProject.Application.Settlement;

/// <summary>
/// Settlement answers "move today's collected money to the settlement
/// account" — an action on our own totals, needing no external comparison.
/// The transfer itself is simulated: the audit entry is the settlement
/// record. Contrast with reconciliation, which compares against Stripe and
/// moves nothing.
/// </summary>
public sealed class SettleEndOfDayHandler(
    IEndOfDaySummaryQuery summaryQuery,
    IAuditLogWriter auditLogWriter)
{
    public async Task<SettlementResult> HandleAsync(
        DateOnly businessDate, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var summaries = await summaryQuery.GetAsync(businessDate, cancellationToken);

        // Plain sum of satang-precise amounts — no division, no rounding.
        var totalCollected = summaries.Sum(summary => summary.TotalCollected);

        await auditLogWriter.WriteAsync(
            new AuditEntry(
                "Settlement",
                businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "EndOfDaySettlement",
                nameof(SettleEndOfDayHandler),
                nowUtc,
                new Dictionary<string, object?>
                {
                    ["businessDate"] = businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["loanCount"] = summaries.Count,
                    // Amounts go in as invariant strings: the audit log is
                    // forensics, not the ledger, and BSON's default mapping
                    // for decimal-inside-object is a trap not worth arming.
                    ["totalCollected"] = totalCollected.ToString(CultureInfo.InvariantCulture),
                    ["perLoan"] = summaries
                        .Select(summary => new Dictionary<string, object?>
                        {
                            ["loanId"] = summary.LoanId.ToString(),
                            ["paymentsCount"] = summary.PaymentsCount,
                            ["totalCollected"] = summary.TotalCollected.ToString(CultureInfo.InvariantCulture),
                        })
                        .ToList(),
                }),
            cancellationToken);

        return new SettlementResult(businessDate, summaries.Count, totalCollected);
    }
}
