using System.Net;
using LoanProject.Application.Secrets;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods.Token;

namespace LoanProject.Infrastructure.Secrets;

/// <summary>
/// Reads secrets from HashiCorp Vault's KV v2 engine. All of the app's
/// secrets live as keys of one document (secret/loan-api), so a lookup
/// reads the document and picks the key.
/// </summary>
public sealed class VaultSecretProvider : ISecretProvider
{
    private readonly IVaultClient _client;
    private readonly string _mountPoint;
    private readonly string _basePath;

    public VaultSecretProvider(string address, string token, string mountPoint = "secret", string basePath = "loan-api")
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Vault address is required.", nameof(address));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Vault token is required.", nameof(token));

        _client = new VaultClient(new VaultClientSettings(address, new TokenAuthMethodInfo(token)));
        _mountPoint = mountPoint;
        _basePath = basePath;
    }

    // VaultSharp's API predates CancellationToken support; the parameter is
    // part of the port's contract and will be honored if the client gains it.
    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var secret = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(_basePath, mountPoint: _mountPoint);

            return secret.Data.Data.TryGetValue(name, out var value) && value is not null
                ? value.ToString()!
                : throw new KeyNotFoundException(
                    $"Secret '{name}' not found under '{_mountPoint}/{_basePath}'. Run scripts/seed-vault-dev.sh for local dev.");
        }
        catch (VaultApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // The whole document is gone, not just this key: the dev Vault keeps
            // secrets in memory, so a container restart wipes them. Translate
            // Vault's opaque 404 ({"errors":[]}) into the same actionable hint a
            // missing key gives, rather than leaking the raw API error upward.
            throw new KeyNotFoundException(
                $"Secret '{name}' not found: path '{_mountPoint}/{_basePath}' does not exist (dev Vault may need seeding). Run scripts/seed-vault-dev.sh for local dev.",
                exception);
        }
    }
}
