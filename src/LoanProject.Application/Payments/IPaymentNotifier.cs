namespace LoanProject.Application.Payments;

/// <summary>
/// Fire-and-forget customer notification (phase 5). Best-effort by contract:
/// implementations must swallow delivery failures — a broker outage may cost
/// a notification, never a recorded payment. The queue exists precisely so
/// the webhook does not wait on (or fail with) the notification channel.
/// </summary>
public interface IPaymentNotifier
{
    Task NotifyPaymentReceivedAsync(PaymentReceivedNotice notice, CancellationToken cancellationToken);
}
