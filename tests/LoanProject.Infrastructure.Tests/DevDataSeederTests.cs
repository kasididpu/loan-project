using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Tests;

public class DevDataSeederTests
{
    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateAnything()
    {
        var loanRepository = new LoanEventStoreRepository(TestDatabase.ConnectionString);

        await using (var firstRun = TestDatabase.CreateContext())
            await new DevDataSeeder(firstRun, loanRepository).SeedAsync(CancellationToken.None);
        await using (var secondRun = TestDatabase.CreateContext())
            await new DevDataSeeder(secondRun, loanRepository).SeedAsync(CancellationToken.None);

        // The seeder uses fixed, well-known ids — idempotence means exactly
        // one row each no matter how many times it runs.
        await using var context = TestDatabase.CreateContext();
        var seededCustomers = await context.Customers
            .Where(c => c.FullName.StartsWith("Seed:"))
            .ToListAsync();
        Assert.Equal(2, seededCustomers.Count);

        var seededLoan = await loanRepository.LoadAsync(DevDataSeeder.SeedLoanId, CancellationToken.None);
        Assert.NotNull(seededLoan);
    }
}
