using LoanProject.Application;
using LoanProject.Application.Payments;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The webhook's use case against real stores: idempotent per Stripe event,
/// exact-amount policy enforced by the aggregate, and both worlds written.
/// </summary>
public class RecordStripePaymentHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Principal = 100_000m;

    private static async Task<Loan> PersistActiveLoanAsync(LoanEventStoreRepository loanRepository)
    {
        var loan = Loan.Originate(Guid.NewGuid(), Guid.NewGuid(), Principal, 0.12m, RateType.Effective, 12, Now);
        loan.Approve("officer-1", Now);
        loan.Disburse(Principal, Now);
        await loanRepository.SaveAsync(loan, CancellationToken.None);
        return loan;
    }

    private static (RecordStripePaymentHandler Handler, LoanDbContext Context, RecordingNotifier Notifier)
        CreateHandler(LoanEventStoreRepository loanRepository)
    {
        var context = TestDatabase.CreateContext();
        var notifier = new RecordingNotifier();
        return (
            new RecordStripePaymentHandler(loanRepository, new PaymentRepository(context), context, notifier),
            context,
            notifier);
    }

    private sealed class RecordingNotifier : IPaymentNotifier
    {
        public List<PaymentReceivedNotice> Notices { get; } = [];

        public Task NotifyPaymentReceivedAsync(PaymentReceivedNotice notice, CancellationToken cancellationToken)
        {
            Notices.Add(notice);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleAsync_VerifiedPayment_WritesLedgerAndPaymentRow()
    {
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = await PersistActiveLoanAsync(loanRepository);
        var stripeEventId = $"evt_wh_{Guid.NewGuid():N}";
        var (handler, context, notifier) = CreateHandler(loanRepository);
        await using var _ = context;

        await handler.HandleAsync(
            new StripePaymentNotification(loan.Id, 1, loan.Schedule![0].Payment, stripeEventId, Now),
            CancellationToken.None);

        var reloaded = await loanRepository.LoadAsync(loan.Id, CancellationToken.None);
        Assert.Equal(4, reloaded!.Version);              // originate+approve+disburse+payment
        Assert.Equal(2, reloaded.NextInstallmentNo);
        await using var readContext = TestDatabase.CreateContext();
        var row = await new PaymentRepository(readContext)
            .FindByStripeEventIdAsync(stripeEventId, CancellationToken.None);
        Assert.NotNull(row);
        Assert.Equal(loan.Schedule![0].Payment, row!.Amount);
        // Notification goes out once, after both stores hold the payment.
        var notice = Assert.Single(notifier.Notices);
        Assert.Equal(stripeEventId, notice.StripeEventId);
    }

    [Fact]
    public async Task HandleAsync_SameEventDeliveredTwice_ProcessesOnce()
    {
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = await PersistActiveLoanAsync(loanRepository);
        var stripeEventId = $"evt_wh_{Guid.NewGuid():N}";
        var notification = new StripePaymentNotification(
            loan.Id, 1, loan.Schedule![0].Payment, stripeEventId, Now);

        var (firstHandler, firstContext, _) = CreateHandler(loanRepository);
        await using (firstContext)
            await firstHandler.HandleAsync(notification, CancellationToken.None);
        // Stripe delivers at-least-once — the second delivery must be a no-op.
        var (secondHandler, secondContext, secondNotifier) = CreateHandler(loanRepository);
        await using (secondContext)
            await secondHandler.HandleAsync(notification, CancellationToken.None);
        Assert.Empty(secondNotifier.Notices); // already processed: no repeat notification

        var reloaded = await loanRepository.LoadAsync(loan.Id, CancellationToken.None);
        Assert.Equal(4, reloaded!.Version); // exactly one PaymentReceived event
    }

    [Fact]
    public async Task HandleAsync_WrongAmount_IsRejectedByTheAggregate()
    {
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = await PersistActiveLoanAsync(loanRepository);
        var (handler, context, notifier) = CreateHandler(loanRepository);
        await using var _ = context;

        // Exact-amount policy: a verified Stripe event still cannot collect
        // the wrong figure — the state machine throws before any write.
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            new StripePaymentNotification(loan.Id, 1, 999.99m, $"evt_wh_{Guid.NewGuid():N}", Now),
            CancellationToken.None));

        var reloaded = await loanRepository.LoadAsync(loan.Id, CancellationToken.None);
        Assert.Equal(3, reloaded!.Version); // nothing was appended
        Assert.Empty(notifier.Notices);     // and no customer was told otherwise
    }
}
