namespace LoanProject.Application.Payments;

/// <summary>
/// The fact a customer notification is built from. StripeEventId doubles as
/// the idempotency key on the consuming side: delivery is at-least-once, so
/// the same notice may arrive twice but must notify only once.
/// </summary>
public sealed record PaymentReceivedNotice(
    Guid LoanId,
    int InstallmentNo,
    decimal Amount,
    string StripeEventId,
    DateTime PaidAtUtc);
