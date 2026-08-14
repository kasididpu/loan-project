namespace LoanProject.Application.Secrets;

/// <summary>
/// The only door to runtime secrets. Business code never knows which vault
/// stands behind it — migrating from HashiCorp Vault to Azure Key Vault
/// means swapping the implementation, nothing else.
/// </summary>
public interface ISecretProvider
{
    /// <summary>Returns the named secret; throws if it does not exist — a missing secret is a configuration error, never a default.</summary>
    Task<string> GetSecretAsync(string name, CancellationToken cancellationToken);
}
