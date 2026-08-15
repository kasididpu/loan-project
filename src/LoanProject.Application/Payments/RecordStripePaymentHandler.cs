using LoanProject.Application.Loans;
using LoanProject.Domain.Payments;

namespace LoanProject.Application.Payments;

/// <summary>
/// The use case behind the Stripe webhook: record a verified payment in
/// both worlds — the loan's event stream (source of truth) and the Payment
/// row (transactional record). Idempotent per Stripe event id, because
/// Stripe delivers at-least-once.
/// </summary>
public sealed class RecordStripePaymentHandler(
    ILoanRepository loanRepository,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPaymentNotifier paymentNotifier)
{
    public async Task HandleAsync(StripePaymentNotification notification, CancellationToken cancellationToken)
    {
        // Idempotency gate: one Stripe event, one payment. The unique index
        // UX_Payment_StripeEventId backs this check mechanically.
        if (await paymentRepository.FindByStripeEventIdAsync(notification.StripeEventId, cancellationToken) is not null)
            return;

        var loan = await loanRepository.LoadAsync(notification.LoanId, cancellationToken)
            ?? throw new InvalidOperationException($"Loan '{notification.LoanId}' does not exist.");

        // Crash-recovery path: the ledger already holds this installment
        // (a previous delivery died between the two writes below). Heal the
        // missing Payment row and stop — the Stripe event id keeps the two
        // worlds linked even though the row is written on a later delivery.
        if (loan.NextInstallmentNo > notification.InstallmentNo)
        {
            paymentRepository.Add(new Payment(
                Guid.NewGuid(), notification.LoanId, notification.Amount,
                notification.StripeEventId, notification.OccurredAtUtc));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Re-notifying here is safe: delivery is at-least-once and the
            // consumer dedupes by StripeEventId.
            await NotifyAsync(notification, cancellationToken);
            return;
        }

        var paymentId = Guid.NewGuid();

        // The aggregate is the referee: exact amount, in order, active loan —
        // anything else throws before either store is touched.
        loan.ReceivePayment(
            paymentId, notification.Amount, notification.InstallmentNo,
            notification.StripeEventId, notification.OccurredAtUtc);

        // Ledger first (source of truth), CRUD record second. Not atomic
        // across stores by design — the recovery branch above heals the gap.
        await loanRepository.SaveAsync(loan, cancellationToken);

        paymentRepository.Add(new Payment(
            paymentId, notification.LoanId, notification.Amount,
            notification.StripeEventId, notification.OccurredAtUtc));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Only after both stores hold the payment: a notification must never
        // announce money the ledger does not have.
        await NotifyAsync(notification, cancellationToken);
    }

    private Task NotifyAsync(StripePaymentNotification notification, CancellationToken cancellationToken) =>
        // Best-effort by the port's contract: a broker outage is logged by
        // the implementation and never fails the webhook.
        paymentNotifier.NotifyPaymentReceivedAsync(
            new PaymentReceivedNotice(
                notification.LoanId, notification.InstallmentNo, notification.Amount,
                notification.StripeEventId, notification.OccurredAtUtc),
            cancellationToken);
}
