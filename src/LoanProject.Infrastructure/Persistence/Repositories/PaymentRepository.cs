using LoanProject.Application.Payments;
using LoanProject.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly LoanDbContext _dbContext;

    public PaymentRepository(LoanDbContext dbContext) => _dbContext = dbContext;

    public void Add(Payment payment) => _dbContext.Payments.Add(payment);

    // AsNoTracking on both reads: Payment is immutable (a transactional
    // record), so tracking it would only cost snapshot memory for nothing.
    public Task<Payment?> FindByStripeEventIdAsync(string stripeEventId, CancellationToken cancellationToken) =>
        _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StripeEventId == stripeEventId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListByLoanAsync(Guid loanId, CancellationToken cancellationToken) =>
        await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.LoanId == loanId)
            .OrderBy(p => p.PaidAtUtc)
            .ToListAsync(cancellationToken);
}
