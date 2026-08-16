namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// Everything <see cref="JwtTokenService"/> needs, assembled once at composition
/// time: the issuer/audience/lifetimes come from configuration (non-secret), the
/// signing key from Vault. Passed as a single value so the token service never
/// reaches into IConfiguration or a secret store itself.
/// </summary>
public sealed record JwtOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    TimeSpan AccessTokenLifetime,
    TimeSpan MfaTokenLifetime);
