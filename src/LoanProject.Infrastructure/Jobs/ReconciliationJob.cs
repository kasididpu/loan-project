using LoanProject.Application.Reconciliation;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanProject.Infrastructure.Jobs;

/// <summary>
/// Scheduled reconciliation: our Payment book vs Stripe's event feed.
/// Scheduling lives in the app (Quartz), not the database — Azure SQL
/// Database has no SQL Agent, so the app-side scheduler is what keeps the
/// optional cloud path viable.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReconciliationJob(
    ReconcileStripePaymentsHandler handler,
    ILogger<ReconciliationJob> logger) : IJob
{
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(1);

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var result = await handler.HandleAsync(
                nowUtc - LookbackWindow, nowUtc, context.CancellationToken);

            if (result.IsClean)
                logger.LogInformation(
                    "Reconciliation clean: {StripeEventCount} stripe event(s) checked, {IgnoredCount} ignored (no loan metadata).",
                    result.StripeEventCount, result.IgnoredCount);
            else
                logger.LogWarning(
                    "Reconciliation found {MissingCount} stripe event(s) with no payment record: {MissingEventIds}",
                    result.MissingEventIds.Count, string.Join(", ", result.MissingEventIds));
        }
        catch (Exception exception)
        {
            // A failed run (Stripe down, key missing from Vault) is logged,
            // not thrown: the next scheduled run is the retry.
            logger.LogError(exception, "Reconciliation run failed.");
        }
    }
}
