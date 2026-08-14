namespace LoanProject.Application.Payments;

/// <summary>
/// A verified, parsed payment event. The Api layer owns Stripe's SDK and
/// signature verification; by the time this record exists, the event is
/// authentic — Application never learns Stripe's wire format.
/// </summary>
public sealed record StripePaymentNotification(
    Guid LoanId,
    int InstallmentNo,
    decimal Amount,
    string StripeEventId,
    DateTime OccurredAtUtc);
