namespace LoanProject.Application.Auth;

/// <summary>
/// Mints and validates the JSON Web Tokens the API issues. The signing key is a
/// secret (from Vault); the concrete implementation in Infrastructure holds it,
/// so nothing in the application or API layer ever touches the raw key.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// A full access token carrying the caller's roles and, for a Customer, the
    /// customer id used to scope their own data.
    /// </summary>
    string IssueAccessToken(Guid subjectId, string subjectName, IReadOnlyCollection<string> roles, Guid? customerId);

    /// <summary>
    /// A short-lived token that proves a password was accepted but MFA is still
    /// outstanding. It carries no roles, so it opens no protected endpoint — only
    /// the OTP-verification step accepts it, exchanging it for a real access token.
    /// </summary>
    string IssueMfaPendingToken(Guid subjectId, string subjectName);

    /// <summary>
    /// Validates an MFA-pending token (signature, lifetime, token-use marker) and
    /// returns the subject it was issued for, or null if it is invalid/expired.
    /// </summary>
    Guid? ReadMfaPendingSubject(string token);

    /// <summary>Access-token lifetime in seconds — surfaced to clients as expires_in.</summary>
    int AccessTokenLifetimeSeconds { get; }
}
