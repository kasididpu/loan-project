namespace LoanProject.Application.Auth;

/// <summary>
/// Authorization policy names. A policy maps one endpoint group to the roles
/// allowed to reach it; endpoints reference the policy, never a raw role list,
/// so the role-to-endpoint mapping lives in exactly one place (Program.cs).
/// </summary>
public static class AuthPolicies
{
    /// <summary>Loan lifecycle commands + customer onboarding (Admin, LoanOfficer).</summary>
    public const string LoanOfficer = "LoanOfficerPolicy";

    /// <summary>KYC decisions (Admin, ComplianceOfficer).</summary>
    public const string Compliance = "CompliancePolicy";

    /// <summary>Portfolio-level reporting — any staff role (Admin, LoanOfficer, ComplianceOfficer).</summary>
    public const string BackOffice = "BackOfficePolicy";
}
