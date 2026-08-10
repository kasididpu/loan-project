namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// PaymentId and StripeEventId link this stream entry back to the Payment
/// record and the Stripe webhook event for end-to-end tracing.
/// </summary>
public sealed record PaymentReceived(
    Guid PaymentId,
    decimal Amount,
    int InstallmentNo,
    string StripeEventId,
    DateTime OccurredAtUtc) : IDomainEvent;
