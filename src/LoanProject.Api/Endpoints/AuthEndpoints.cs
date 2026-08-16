using System.Security.Cryptography;
using LoanProject.Application.Auth;
using LoanProject.Infrastructure.Auth;
using LoanProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Authentication endpoints (Phase 8). Three ways to obtain a bearer token:
/// password login (with optional OTP second factor), OTP verification, and the
/// OAuth 2.0 client-credentials flow for system-to-system callers. All are
/// anonymous by definition — they are how a caller becomes authenticated.
/// </summary>
public static class AuthEndpoints
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");
        group.MapPost("/login", LoginAsync);
        group.MapPost("/verify-otp", VerifyOtpAsync);
        // OAuth token endpoint takes a form-encoded body per the spec; form
        // binding needs antiforgery turned off for this non-browser call.
        group.MapPost("/token", ClientCredentialsAsync).DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<AppUser> users,
        IPasswordHasher<AppUser> passwordHasher,
        IJwtTokenService tokens,
        IOtpStore otpStore,
        IWebHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(request.Username);
        var passwordValid = user is not null && await users.CheckPasswordAsync(user, request.Password);

        // Equalise timing so a missing user is indistinguishable from a wrong
        // password by response latency: when there is no user, burn the same
        // PBKDF2 work CheckPasswordAsync would have done, closing the
        // username-enumeration oracle. Same generic failure either way.
        if (user is null)
            _ = passwordHasher.HashPassword(new AppUser(), request.Password);
        if (!passwordValid)
            return Unauthorized("invalid_credentials");

        if (user!.MfaEnabled)
        {
            var code = GenerateOtp();
            await otpStore.StoreAsync(user.Id, code, OtpLifetime, cancellationToken);

            // Stub delivery: a real system sends the code by SMS/email. It is
            // written to the log ONLY in Development so the flow is demonstrable —
            // an OTP in a production log would defeat MFA, so it is gated on the
            // environment, not on a seeding convention.
            if (environment.IsDevelopment())
                loggerFactory.CreateLogger("Auth.Otp").LogWarning(
                    "DEV OTP for {User}: {Code} (valid {Minutes} min)", user.UserName, code, OtpLifetime.TotalMinutes);

            return Results.Ok(new { mfaRequired = true, mfaToken = tokens.IssueMfaPendingToken(user.Id, user.UserName!) });
        }

        return Results.Ok(await IssueForUserAsync(user, users, tokens));
    }

    private static async Task<IResult> VerifyOtpAsync(
        VerifyOtpRequest request,
        UserManager<AppUser> users,
        IJwtTokenService tokens,
        IOtpStore otpStore,
        CancellationToken cancellationToken)
    {
        var subject = tokens.ReadMfaPendingSubject(request.MfaToken);
        if (subject is null)
            return Unauthorized("invalid_mfa_token");

        if (!await otpStore.ValidateAndConsumeAsync(subject.Value, request.Code, cancellationToken))
            return Unauthorized("invalid_or_expired_code");

        var user = await users.FindByIdAsync(subject.Value.ToString());
        if (user is null)
            return Unauthorized("invalid_mfa_token");

        return Results.Ok(await IssueForUserAsync(user, users, tokens));
    }

    private static async Task<IResult> ClientCredentialsAsync(
        [FromForm] string grant_type,
        [FromForm] string client_id,
        [FromForm] string client_secret,
        LoanDbContext db,
        IJwtTokenService tokens,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(grant_type, "client_credentials", StringComparison.Ordinal))
            return Results.BadRequest(new { error = "unsupported_grant_type" });

        var client = await db.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == client_id, cancellationToken);
        // Same generic "invalid_client" whether the id is unknown or the secret
        // is wrong — do not let a caller probe which client ids exist.
        if (client is null)
            return Unauthorized("invalid_client");

        var verification = new PasswordHasher<OAuthClient>()
            .VerifyHashedPassword(client, client.ClientSecretHash, client_secret);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized("invalid_client");

        var token = tokens.IssueAccessToken(client.Id, client.ClientId, new[] { client.Role }, customerId: null);
        return Results.Ok(new AccessTokenResponse(token, "Bearer", tokens.AccessTokenLifetimeSeconds));
    }

    private static async Task<AccessTokenResponse> IssueForUserAsync(
        AppUser user, UserManager<AppUser> users, IJwtTokenService tokens)
    {
        var roles = await users.GetRolesAsync(user);
        var token = tokens.IssueAccessToken(user.Id, user.UserName!, roles.ToArray(), user.CustomerId);
        return new AccessTokenResponse(token, "Bearer", tokens.AccessTokenLifetimeSeconds);
    }

    // Six-digit numeric code from a cryptographic RNG.
    private static string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static IResult Unauthorized(string error) =>
        Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);
}
