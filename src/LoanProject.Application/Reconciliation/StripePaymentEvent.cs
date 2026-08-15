namespace LoanProject.Application.Reconciliation;

/// <summary>
/// One successful payment event as reported by Stripe. LoanId/InstallmentNo
/// are null when the event carries no loan metadata — it exists in the same
/// Stripe test account but was never ours to record, so reconciliation
/// counts it as ignored rather than missing.
/// </summary>
public sealed record StripePaymentEvent(
    string EventId,
    Guid? LoanId,
    int? InstallmentNo,
    decimal Amount,
    DateTime CreatedUtc);
