using LoanProject.Application.Audit;
using LoanProject.Application.Payments;
using LoanProject.Application.Reconciliation;
using LoanProject.Domain.Payments;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Reconciliation logic with in-memory ports: flags Stripe events missing
/// from our book, ignores events that were never ours, and always leaves an
/// audit trail. No network, no database — the comparison is the unit.
/// </summary>
public class ReconcileStripePaymentsHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static StripePaymentEvent OurEvent(string eventId) =>
        new(eventId, Guid.NewGuid(), 1, 8884.88m, Now);

    private static StripePaymentEvent ForeignEvent(string eventId) =>
        new(eventId, LoanId: null, InstallmentNo: null, 20m, Now);

    [Fact]
    public async Task HandleAsync_EveryStripeEventHasAPaymentRow_ReportsClean()
    {
        var recorded = OurEvent("evt_rec_1");
        var handler = new ReconcileStripePaymentsHandler(
            new FakeStripeSource(recorded),
            new FakePayments("evt_rec_1"),
            new FakeAudit());

        var result = await handler.HandleAsync(Now.AddDays(-1), Now, CancellationToken.None);

        Assert.True(result.IsClean);
        Assert.Equal(1, result.StripeEventCount);
        Assert.Equal(0, result.IgnoredCount);
    }

    [Fact]
    public async Task HandleAsync_StripeEventWithoutPaymentRow_IsFlaggedMissing()
    {
        var handler = new ReconcileStripePaymentsHandler(
            new FakeStripeSource(OurEvent("evt_rec_lost"), OurEvent("evt_rec_ok")),
            new FakePayments("evt_rec_ok"),
            new FakeAudit());

        var result = await handler.HandleAsync(Now.AddDays(-1), Now, CancellationToken.None);

        Assert.False(result.IsClean);
        Assert.Equal(new[] { "evt_rec_lost" }, result.MissingEventIds);
    }

    [Fact]
    public async Task HandleAsync_EventWithoutLoanMetadata_IsIgnoredNotMissing()
    {
        var handler = new ReconcileStripePaymentsHandler(
            new FakeStripeSource(ForeignEvent("evt_rec_foreign")),
            new FakePayments(),
            new FakeAudit());

        var result = await handler.HandleAsync(Now.AddDays(-1), Now, CancellationToken.None);

        // A fixture default or another experiment in the same test account
        // is not a discrepancy in our book.
        Assert.True(result.IsClean);
        Assert.Equal(1, result.IgnoredCount);
    }

    [Fact]
    public async Task HandleAsync_EveryRun_WritesOneAuditEntry()
    {
        var audit = new FakeAudit();
        var handler = new ReconcileStripePaymentsHandler(
            new FakeStripeSource(OurEvent("evt_rec_lost")),
            new FakePayments(),
            audit);

        await handler.HandleAsync(Now.AddDays(-1), Now, CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("Reconciliation", entry.EntityType);
        Assert.Equal("StripeReconciliationRun", entry.Action);
        Assert.False((bool)entry.Details["isClean"]!);
    }

    private sealed class FakeStripeSource(params StripePaymentEvent[] events) : IStripeEventSource
    {
        public Task<IReadOnlyList<StripePaymentEvent>> ListRecentPaymentEventsAsync(
            DateTime sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StripePaymentEvent>>(events);
    }

    private sealed class FakePayments(params string[] knownStripeEventIds) : IPaymentRepository
    {
        public void Add(Payment payment) => throw new NotSupportedException("Reconciliation never writes.");

        public Task<Payment?> FindByStripeEventIdAsync(string stripeEventId, CancellationToken cancellationToken) =>
            Task.FromResult(knownStripeEventIds.Contains(stripeEventId)
                ? new Payment(Guid.NewGuid(), Guid.NewGuid(), 8884.88m, stripeEventId, Now)
                : null);

        public Task<IReadOnlyList<Payment>> ListByLoanAsync(Guid loanId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Payment>>([]);
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
