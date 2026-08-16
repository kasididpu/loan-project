using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoanProject.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// Issues and validates HS256 JWTs. Claims are written with their raw JWT names
/// (sub, unique_name, role, customer_id) and consumed the same way — the bearer
/// middleware is configured with MapInboundClaims off, so no name is silently
/// rewritten to a long ClaimTypes URI.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>Role claim name — matches TokenValidationParameters.RoleClaimType.</summary>
    public const string RoleClaim = "role";

    /// <summary>Custom claim carrying the customer a borrower login represents.</summary>
    public const string CustomerIdClaim = "customer_id";

    // Marks the MFA half-token so the OTP step can refuse to treat it as a real
    // access token (and vice versa).
    private const string TokenUseClaim = "token_use";
    private const string MfaPendingTokenUse = "mfa_pending";

    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
            throw new ArgumentException("JWT signing key is required.", nameof(options));

        _options = options;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }

    public int AccessTokenLifetimeSeconds => (int)_options.AccessTokenLifetime.TotalSeconds;

    public string IssueAccessToken(
        Guid subjectId, string subjectName, IReadOnlyCollection<string> roles, Guid? customerId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, subjectName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        foreach (var role in roles)
            claims.Add(new Claim(RoleClaim, role));
        if (customerId is { } id)
            claims.Add(new Claim(CustomerIdClaim, id.ToString()));

        return WriteToken(claims, _options.AccessTokenLifetime);
    }

    public string IssueMfaPendingToken(Guid subjectId, string subjectName) =>
        WriteToken(
            new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, subjectName),
                new(TokenUseClaim, MfaPendingTokenUse),
            },
            _options.MfaTokenLifetime);

    public Guid? ReadMfaPendingSubject(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(token, ValidationParameters(), out _);
            if (principal.FindFirstValue(TokenUseClaim) != MfaPendingTokenUse)
                return null; // an access token must not pass as an MFA token

            return Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
        }
        catch (Exception)
        {
            // Any validation failure (bad signature, expired, malformed) simply
            // means "not a usable MFA token" — never surface the reason.
            return null;
        }
    }

    /// <summary>
    /// The parameters both the bearer middleware and the MFA-token check validate
    /// against. Names are kept raw (RoleClaimType = "role"), so authorization and
    /// ICurrentUser read exactly what was issued.
    /// </summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.UniqueName,
        RoleClaimType = RoleClaim,
    };

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
