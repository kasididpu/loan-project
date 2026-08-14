using LoanProject.Infrastructure.Secrets;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Integration tests against the dev-mode Vault container. Each test seeds
/// its own unique path, same isolation philosophy as the other suites.
/// </summary>
public class VaultSecretProviderTests
{
    private static string Address =>
        Environment.GetEnvironmentVariable("Vault__Address") ?? "http://localhost:8200";

    private static string Token =>
        Environment.GetEnvironmentVariable("Vault__Token") ?? "loan-dev-root";

    private static Task SeedAsync(string basePath, Dictionary<string, object> data)
    {
        var client = new VaultClient(new VaultClientSettings(Address, new TokenAuthMethodInfo(Token)));
        return client.V1.Secrets.KeyValue.V2.WriteSecretAsync(basePath, data, mountPoint: "secret");
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValueSeededIntoVault()
    {
        var basePath = $"it-{Guid.NewGuid():N}";
        await SeedAsync(basePath, new Dictionary<string, object> { ["LoanDb"] = "vault-value-1" });

        var provider = new VaultSecretProvider(Address, Token, basePath: basePath);
        var value = await provider.GetSecretAsync("LoanDb", CancellationToken.None);

        Assert.Equal("vault-value-1", value);
    }

    [Fact]
    public async Task GetSecretAsync_UnknownName_ThrowsInsteadOfDefaulting()
    {
        var basePath = $"it-{Guid.NewGuid():N}";
        await SeedAsync(basePath, new Dictionary<string, object> { ["Existing"] = "x" });

        var provider = new VaultSecretProvider(Address, Token, basePath: basePath);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => provider.GetSecretAsync("Missing", CancellationToken.None));
    }
}
