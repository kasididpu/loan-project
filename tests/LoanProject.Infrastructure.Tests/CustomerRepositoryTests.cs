using LoanProject.Application;
using LoanProject.Domain.Customers;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

public class CustomerRepositoryTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsAndReadsBack()
    {
        var customer = new Customer(Guid.NewGuid(), "Somchai Jaidee", Now);

        // Write through one context...
        await using (var writeContext = TestDatabase.CreateContext())
        {
            var repository = new CustomerRepository(writeContext);
            repository.Add(customer);

            // The unit of work IS the context — this is the only I/O moment.
            IUnitOfWork unitOfWork = writeContext;
            var written = await unitOfWork.SaveChangesAsync(CancellationToken.None);
            Assert.Equal(1, written);
        }

        // ...read back through a fresh one, so nothing comes from the tracker cache.
        await using var readContext = TestDatabase.CreateContext();
        var found = await new CustomerRepository(readContext).FindAsync(customer.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(customer.FullName, found!.FullName);
        Assert.Equal(customer.CreatedAtUtc, found.CreatedAtUtc);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        await using var context = TestDatabase.CreateContext();

        var found = await new CustomerRepository(context).FindAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}
