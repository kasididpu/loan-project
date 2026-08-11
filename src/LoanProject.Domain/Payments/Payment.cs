namespace LoanProject.Domain.Payments;

/// <summary>
/// Transactional record of one received payment — conventional CRUD entity
/// (not event-sourced): it happens once and never transitions. The Loan
/// stream references it by PaymentId; StripeEventId links back to the
/// webhook event for end-to-end tracing.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; }
    public Guid LoanId { get; }
    public decimal Amount { get; }
    public string StripeEventId { get; }
    public DateTime PaidAtUtc { get; }

    public Payment(Guid id, Guid loanId, decimal amount, string stripeEventId, DateTime paidAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Payment id must not be empty.", nameof(id));
        if (loanId == Guid.Empty)
            throw new ArgumentException("Loan id must not be empty.", nameof(loanId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
        if (amount % 0.01m != 0m)
            throw new ArgumentException("Amount must not be finer than satang (2 decimal places).", nameof(amount));
        if (string.IsNullOrWhiteSpace(stripeEventId))
            throw new ArgumentException("Stripe event id is required.", nameof(stripeEventId));

        Id = id;
        LoanId = loanId;
        Amount = amount;
        StripeEventId = stripeEventId;
        PaidAtUtc = paidAtUtc;
    }
}
