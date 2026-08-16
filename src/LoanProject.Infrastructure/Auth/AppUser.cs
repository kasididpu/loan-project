using Microsoft.AspNetCore.Identity;

namespace LoanProject.Infrastructure.Auth;

/// <summary>
/// The authentication identity, kept separate from the Customer domain aggregate:
/// a staff member has an AppUser but no Customer, and a borrower's AppUser links
/// to their Customer through <see cref="CustomerId"/>. That id flows into the
/// access token so the API can scope a Customer caller to their own data. MFA is
/// opt-in per user so the OTP step can be demonstrated on some logins, not all.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public Guid? CustomerId { get; set; }

    public bool MfaEnabled { get; set; }
}
