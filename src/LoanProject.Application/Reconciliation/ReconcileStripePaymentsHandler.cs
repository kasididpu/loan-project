using LoanProject.Application.Audit;
using LoanProject.Application.Payments;

namespace LoanProject.Application.Reconciliation;

/// <summary>
/// Reconciliation answers "does our book match Stripe's?" — it compares and
/// flags, it never fixes. A Stripe success event with loan metadata but no
/// Payment row means a webhook delivery was lost or failed; the mismatch
/// goes to the audit log for a human, because writing money records is the
/// webhook path's job alone.
/// </summary>
public sealed class ReconcileStripePaymentsHandler(
    IStripeEventSource stripeEventSource,
    IPaymentRepository paymentRepository,
    IAuditLogWriter auditLogWriter)
{
    public async Task<ReconciliationResult> HandleAsync(
        DateTime sinceUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var stripeEvents = await stripeEventSource.ListRecentPaymentEventsAsync(sinceUtc, cancellationToken);

        var ignored = 0;
        var missing = new List<string>();
        foreach (var stripeEvent in stripeEvents)
        {
            // No loan metadata: not created by this system — nothing to reconcile.
            if (stripeEvent.LoanId is null)
            {
                ignored++;
                continue;
            }

            if (await paymentRepository.FindByStripeEventIdAsync(stripeEvent.EventId, cancellationToken) is null)
                missing.Add(stripeEvent.EventId);
        }

        var result = new ReconciliationResult(stripeEvents.Count, ignored, missing);

        await auditLogWriter.WriteAsync(
            new AuditEntry(
                "Reconciliation",
                nowUtc.ToString("yyyy-MM-dd"),
                "StripeReconciliationRun",
                nameof(ReconcileStripePaymentsHandler),
                nowUtc,
                new Dictionary<string, object?>
                {
                    ["sinceUtc"] = sinceUtc,
                    ["stripeEventCount"] = result.StripeEventCount,
                    ["ignoredCount"] = result.IgnoredCount,
                    ["missingEventIds"] = result.MissingEventIds.ToList(),
                    ["isClean"] = result.IsClean,
                }),
            cancellationToken);

        return result;
    }
}
