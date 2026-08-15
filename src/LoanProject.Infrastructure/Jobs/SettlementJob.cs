using LoanProject.Application.Settlement;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanProject.Infrastructure.Jobs;

/// <summary>
/// Scheduled end-of-day settlement: aggregate today's collections (via the
/// stored procedure behind IEndOfDaySummaryQuery) and simulate the transfer
/// to the settlement account. App-side scheduler for the same reason as
/// <see cref="ReconciliationJob"/> — Azure SQL Database has no SQL Agent.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SettlementJob(
    SettleEndOfDayHandler handler,
    ILogger<SettlementJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var result = await handler.HandleAsync(
                DateOnly.FromDateTime(nowUtc), nowUtc, context.CancellationToken);

            logger.LogInformation(
                "Settlement simulated for {BusinessDate}: {LoanCount} loan(s), total {TotalCollected} transferred.",
                result.BusinessDate, result.LoanCount, result.TotalCollected);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Settlement run failed.");
        }
    }
}
