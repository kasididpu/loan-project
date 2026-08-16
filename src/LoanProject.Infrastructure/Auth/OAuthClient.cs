namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// A registered non-human API consumer for the OAuth 2.0 client-credentials
/// flow. The secret is stored only as a hash (same PBKDF2 hasher Identity uses
/// for passwords); the granted role is embedded in the issued token, so a client
/// receives exactly the access it was provisioned for and nothing more.
/// </summary>
public sealed class OAuthClient
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
