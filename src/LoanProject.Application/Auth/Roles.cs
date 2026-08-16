namespace LoanProject.Application.Auth;

/// <summary>
/// The fixed set of role names. Centralised so the seeder, the authorization
/// policies and any in-handler role check all spell them the same way — a typo
/// in a magic string is a silent authorization hole.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string LoanOfficer = "LoanOfficer";
    public const string ComplianceOfficer = "ComplianceOfficer";
    public const string Customer = "Customer";

    /// <summary>Non-human caller authenticated through OAuth client credentials.</summary>
    public const string System = "System";
}
