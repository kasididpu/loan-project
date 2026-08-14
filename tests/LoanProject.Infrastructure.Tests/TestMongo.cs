using MongoDB.Driver;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Same philosophy as TestDatabase: real MongoDB container (docker compose
/// must be up), fresh ids per test, nothing cleaned up.
/// </summary>
internal static class TestMongo
{
    public static IMongoDatabase Database { get; } =
        new MongoClient(
                Environment.GetEnvironmentVariable("ConnectionStrings__Mongo")
                ?? "mongodb://root:LoanDevMongo1@localhost:27017")
            .GetDatabase("LoanProject");
}
