using LoanProject.Application;
using LoanProject.Domain.Payments;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

public class PaymentRepositoryTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Add_ThenFindByStripeEventId_ReturnsPayment()
    {
        // Stripe event ids are globally unique — fresh one per run keeps the
        // shared dev database append-friendly, same as the ledger tests.
        var stripeEventId = $"evt_it_{Guid.NewGuid():N}";
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 8_884.88m, stripeEventId, Now);

        await using (var writeContext = TestDatabase.CreateContext())
        {
            new PaymentRepository(writeContext).Add(payment);
            await ((IUnitOfWork)writeContext).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = TestDatabase.CreateContext();
        var found = await new PaymentRepository(readContext)
            .FindByStripeEventIdAsync(stripeEventId, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(payment.Id, found!.Id);
        Assert.Equal(8_884.88m, found.Amount);
    }

    [Fact]
    public async Task ListByLoanAsync_ReturnsOnlyThatLoansPaymentsInPaidOrder()
    {
        var loanId = Guid.NewGuid();
        var otherLoanId = Guid.NewGuid();
        var second = new Payment(Guid.NewGuid(), loanId, 8_884.88m, $"evt_it_{Guid.NewGuid():N}", Now.AddMonths(1));
        var first = new Payment(Guid.NewGuid(), loanId, 8_884.88m, $"evt_it_{Guid.NewGuid():N}", Now);
        var foreign = new Payment(Guid.NewGuid(), otherLoanId, 500m, $"evt_it_{Guid.NewGuid():N}", Now);

        await using (var writeContext = TestDatabase.CreateContext())
        {
            var repository = new PaymentRepository(writeContext);
            repository.Add(second); // inserted out of order on purpose
            repository.Add(first);
            repository.Add(foreign);
            await ((IUnitOfWork)writeContext).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = TestDatabase.CreateContext();
        var payments = await new PaymentRepository(readContext).ListByLoanAsync(loanId, CancellationToken.None);

        Assert.Equal(2, payments.Count);
        Assert.Equal(first.Id, payments[0].Id); // ordered by PaidAtUtc, not insert order
        Assert.Equal(second.Id, payments[1].Id);
    }
}
