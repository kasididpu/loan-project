namespace LoanProject.Api.Tests;

/// <summary>
/// All HTTP tests share one application instance (one boot, one migrate/seed) and
/// run sequentially within this collection — so parallel app instances never race
/// on the same database's migration/seed.
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
