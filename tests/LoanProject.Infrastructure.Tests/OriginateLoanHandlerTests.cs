using LoanProject.Application.Audit;
using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Rates;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Command side against the real event store. Confirms the rate is taken from
/// the sheet (not the caller), the customer id is carried onto the aggregate,
/// and the AML review flag fires exactly at/above the threshold.
/// </summary>
public class OriginateLoanHandlerTests
{
    // In-memory audit writer so the AML flag can be asserted without Mongo.
    private sealed class SpyAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = new();

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> ListByEntityAsync(
            string entityType, string entityId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(Entries);
    }

    private static OriginateLoanHandler NewHandler(IAuditLogWriter audit) =>
        new(new LoanEventStoreRepository(TestDatabase.ConnectionString), new StaticRateSheet(), audit, new FakeCurrentUser());

    [Fact]
    public async Task HandleAsync_LooksUpRate_AppendsOriginatedEvent()
    {
        var rates = new StaticRateSheet();
        var repository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var handler = new OriginateLoanHandler(repository, rates, new SpyAuditLogWriter(), new FakeCurrentUser());
        var customerId = Guid.NewGuid();

        var loanId = await handler.HandleAsync(customerId, 80_000m, RateType.Effective, 12, CancellationToken.None);
        var expectedRate = await rates.GetAnnualRateAsync(RateType.Effective, 12, CancellationToken.None);

        var loan = await repository.LoadAsync(loanId, CancellationToken.None);
        Assert.NotNull(loan);
        Assert.Equal(LoanStatus.Originated, loan!.Status);
        Assert.Equal(customerId, loan.CustomerId);          // carried onto the aggregate (phase 7)
        Assert.Equal(80_000m, loan.Principal);
        Assert.Equal(expectedRate, loan.AnnualRate);        // from the sheet, not the caller
    }

    [Fact]
    public async Task HandleAsync_PrincipalAtThreshold_RaisesAmlFlag()
    {
        var audit = new SpyAuditLogWriter();

        var loanId = await NewHandler(audit).HandleAsync(
            Guid.NewGuid(), OriginateLoanHandler.AmlReviewThresholdBaht, RateType.Effective, 12, CancellationToken.None);

        var flag = Assert.Single(audit.Entries);
        Assert.Equal("AmlReviewFlagged", flag.Action);
        Assert.Equal(loanId.ToString(), flag.EntityId);
    }

    [Fact]
    public async Task HandleAsync_PrincipalBelowThreshold_NoAmlFlag()
    {
        var audit = new SpyAuditLogWriter();

        await NewHandler(audit).HandleAsync(
            Guid.NewGuid(), OriginateLoanHandler.AmlReviewThresholdBaht - 1m, RateType.Effective, 12, CancellationToken.None);

        Assert.Empty(audit.Entries);
    }
}
