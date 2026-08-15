using LoanProject.Application.Audit;
using LoanProject.Application.Reports;
using LoanProject.Application.Settlement;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Settlement logic with in-memory ports: totals the day's collections and
/// records the simulated transfer in the audit trail.
/// </summary>
public class SettleEndOfDayHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateOnly BusinessDate = new(2026, 8, 14);

    [Fact]
    public async Task HandleAsync_TwoLoansCollected_TotalsExactly()
    {
        var handler = new SettleEndOfDayHandler(
            new FakeSummaryQuery(
                new EndOfDayLoanSummary(Guid.NewGuid(), 1, 8884.88m, Now),
                new EndOfDayLoanSummary(Guid.NewGuid(), 2, 17_769.76m, Now)),
            new FakeAudit());

        var result = await handler.HandleAsync(BusinessDate, Now, CancellationToken.None);

        Assert.Equal(2, result.LoanCount);
        // Plain sum of satang-precise amounts — no rounding anywhere.
        Assert.Equal(26_654.64m, result.TotalCollected);
    }

    [Fact]
    public async Task HandleAsync_NothingCollected_StillWritesTheAuditRecord()
    {
        var audit = new FakeAudit();
        var handler = new SettleEndOfDayHandler(new FakeSummaryQuery(), audit);

        var result = await handler.HandleAsync(BusinessDate, Now, CancellationToken.None);

        // "Nothing to transfer today" is itself a fact operations need.
        Assert.Equal(0m, result.TotalCollected);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("Settlement", entry.EntityType);
        Assert.Equal("2026-08-14", entry.EntityId);
        Assert.Equal("0", entry.Details["totalCollected"]);
    }

    [Fact]
    public async Task HandleAsync_AuditDetails_CarryAmountsAsInvariantText()
    {
        var audit = new FakeAudit();
        var handler = new SettleEndOfDayHandler(
            new FakeSummaryQuery(new EndOfDayLoanSummary(Guid.NewGuid(), 1, 8884.88m, Now)),
            audit);

        await handler.HandleAsync(BusinessDate, Now, CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("8884.88", entry.Details["totalCollected"]);
        Assert.Equal(1, entry.Details["loanCount"]);
    }

    private sealed class FakeSummaryQuery(params EndOfDayLoanSummary[] summaries) : IEndOfDaySummaryQuery
    {
        public Task<IReadOnlyList<EndOfDayLoanSummary>> GetAsync(
            DateOnly asOfDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EndOfDayLoanSummary>>(summaries);
    }

    private sealed class FakeAudit : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> ListByEntityAsync(
            string entityType, string entityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>(Entries);
    }
}
