using LoanProject.Application.Auth;
using LoanProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// Development-only auth seed: the four roles, one demo user per role, and one
/// OAuth client for the client-credentials flow. Idempotent — every step checks
/// existence first. All credential values are passed in (they come from Vault),
/// so no password or client secret is ever written into the repository.
/// The "somsri" borrower login is wired to the seeded customer that owns a loan,
/// so IDOR scoping can be shown: she sees her loan, not other customers'.
/// </summary>
public sealed class AuthDataSeeder
{
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly LoanDbContext _db;

    public AuthDataSeeder(UserManager<AppUser> users, RoleManager<IdentityRole<Guid>> roles, LoanDbContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    public async Task SeedAsync(
        string seedPassword, string oauthClientId, string oauthClientSecret, CancellationToken cancellationToken)
    {
        foreach (var role in new[] { Roles.Admin, Roles.LoanOfficer, Roles.ComplianceOfficer, Roles.Customer })
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new IdentityRole<Guid>(role));

        await EnsureUserAsync("admin", seedPassword, Roles.Admin, customerId: null, mfa: false);
        await EnsureUserAsync("officer", seedPassword, Roles.LoanOfficer, customerId: null, mfa: true); // MFA demo login
        await EnsureUserAsync("compliance", seedPassword, Roles.ComplianceOfficer, customerId: null, mfa: false);
        await EnsureUserAsync(
            "somsri", seedPassword, Roles.Customer, customerId: DevDataSeeder.SeedCustomerWithLoanId, mfa: false);
        // A second borrower tied to a different customer, so IDOR scoping is
        // demonstrable: somchai must not see somsri's loan/customer record.
        await EnsureUserAsync(
            "somchai", seedPassword, Roles.Customer, customerId: DevDataSeeder.SeedCustomerNewId, mfa: false);

        await EnsureOAuthClientAsync(oauthClientId, oauthClientSecret, Roles.System, cancellationToken);
    }

    private async Task EnsureUserAsync(string userName, string password, string role, Guid? customerId, bool mfa)
    {
        if (await _users.FindByNameAsync(userName) is not null)
            return;

        var user = new AppUser
        {
            UserName = userName,
            Email = $"{userName}@loan.local",
            EmailConfirmed = true,
            CustomerId = customerId,
            MfaEnabled = mfa,
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Failed to seed user '{userName}': {string.Join("; ", created.Errors.Select(e => e.Description))}");

        await _users.AddToRoleAsync(user, role);
    }

    private async Task EnsureOAuthClientAsync(
        string clientId, string clientSecret, string role, CancellationToken cancellationToken)
    {
        if (await _db.OAuthClients.AnyAsync(c => c.ClientId == clientId, cancellationToken))
            return;

        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Role = role,
            DisplayName = "Reporting bot (client-credentials demo)",
        };
        // Hash the secret with the same hasher Identity uses for passwords.
        client.ClientSecretHash = new PasswordHasher<OAuthClient>().HashPassword(client, clientSecret);

        _db.OAuthClients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
