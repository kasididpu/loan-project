using LoanProject.Application.Customers;
using LoanProject.Domain.Customers;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

/// <summary>KYC status update against the real write DB.</summary>
public class SetKycStatusHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_ChangesStatus_PersistsAndReadsBack()
    {
        var id = Guid.NewGuid();
        await using (var db = TestDatabase.CreateContext())
        {
            db.Customers.Add(new Customer(id, "Pending Person", Now)); // starts Pending
            await db.SaveChangesAsync();
        }

        await using (var db = TestDatabase.CreateContext())
        {
            await new SetKycStatusHandler(new CustomerRepository(db), db)
                .HandleAsync(id, KycStatus.Verified, CancellationToken.None);
        }

        await using var read = TestDatabase.CreateContext();
        var customer = await new CustomerRepository(read).FindAsync(id, CancellationToken.None);
        Assert.Equal(KycStatus.Verified, customer!.KycStatus);
    }

    [Fact]
    public async Task HandleAsync_UnknownCustomer_Throws()
    {
        await using var db = TestDatabase.CreateContext();
        var handler = new SetKycStatusHandler(new CustomerRepository(db), db);

        await Assert.ThrowsAsync<CustomerNotFoundException>(
            () => handler.HandleAsync(Guid.NewGuid(), KycStatus.Verified, CancellationToken.None));
    }
}
