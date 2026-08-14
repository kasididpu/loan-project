using LoanProject.Domain.Payments;

namespace LoanProject.Application.Payments;

public interface IPaymentRepository
{
    /// <summary>Synchronous on purpose — see <see cref="Customers.ICustomerRepository.Add"/>.</summary>
    void Add(Payment payment);

    /// <summary>Webhook idempotency lookup (phase 4): one Stripe event, one payment.</summary>
    Task<Payment?> FindByStripeEventIdAsync(string stripeEventId, CancellationToken cancellationToken);

    /// <summary>Statement view: a loan's payments in the order they were made.</summary>
    Task<IReadOnlyList<Payment>> ListByLoanAsync(Guid loanId, CancellationToken cancellationToken);
}
