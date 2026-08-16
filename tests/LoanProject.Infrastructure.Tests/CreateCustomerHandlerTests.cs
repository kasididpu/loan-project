using LoanProject.Application.Customers;
using LoanProject.Domain.Customers;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

/// <summary>Customer onboarding against the real write DB, including PII round-trip.</summary>
public class CreateCustomerHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsCustomer_StartsPending_AndPiiDecryptsOnRead()
    {
        await using var db = TestDatabase.CreateContext();
        var handler = new CreateCustomerHandler(new CustomerRepository(db), db);

        var created = await handler.HandleAsync(
            "New Person", "1112223334445", "555-5-55555-5", CancellationToken.None);

        await using var read = TestDatabase.CreateContext();
        var loaded = await new CustomerRepository(read).FindAsync(created.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("New Person", loaded!.FullName);
        Assert.Equal(KycStatus.Pending, loaded.KycStatus);       // onboarding starts unverified
        Assert.Equal("1112223334445", loaded.NationalId);        // encrypted at rest, decrypts here
        Assert.Equal("555-5-55555-5", loaded.BankAccountNumber);
    }
}
