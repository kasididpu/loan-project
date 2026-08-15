namespace LoanProject.Application.Reconciliation;

/// <summary>
/// Read-only view of Stripe's event feed for reconciliation. A port so the
/// comparison logic can be tested without the network — and so the Stripe
/// SDK stays an infrastructure detail.
/// </summary>
public interface IStripeEventSource
{
    /// <summary>Successful payment events created at or after the given instant.</summary>
    Task<IReadOnlyList<StripePaymentEvent>> ListRecentPaymentEventsAsync(
        DateTime sinceUtc, CancellationToken cancellationToken);
}
