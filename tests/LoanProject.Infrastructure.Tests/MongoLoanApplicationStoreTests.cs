using LoanProject.Application.LoanApplications;
using LoanProject.Infrastructure.Mongo;

namespace LoanProject.Infrastructure.Tests;

public class MongoLoanApplicationStoreTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAsync_ThenFind_RoundTripsFlexibleFields()
    {
        var store = new MongoLoanApplicationStore(TestMongo.Database);
        // A car-loan form — its fields exist nowhere in any schema.
        var document = new LoanApplicationDocument(Guid.NewGuid(), Guid.NewGuid(), Now,
            new Dictionary<string, object?>
            {
                ["carBrand"] = "Toyota",
                ["carYear"] = 2024,
                ["hasCoSigner"] = false,
            });

        await store.SaveAsync(document, CancellationToken.None);
        var found = await store.FindAsync(document.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(document.CustomerId, found!.CustomerId);
        Assert.Equal(document.SubmittedAtUtc, found.SubmittedAtUtc);
        Assert.Equal("Toyota", found.Fields["carBrand"]);
        Assert.Equal(2024, found.Fields["carYear"]);
        Assert.Equal(false, found.Fields["hasCoSigner"]);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        var store = new MongoLoanApplicationStore(TestMongo.Database);

        var found = await store.FindAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}
