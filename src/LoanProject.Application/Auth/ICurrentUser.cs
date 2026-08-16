namespace LoanProject.Application.Auth;

/// <summary>
/// The authenticated caller of the current request, as seen by the application
/// layer. Handlers depend on this abstraction instead of reading HTTP claims
/// directly, so the officer recorded on a command (approvedBy, disbursedBy) is
/// the verified identity from the token — never a value the caller typed into a
/// request body. The HTTP-aware implementation lives in the API layer.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The authenticated user's id (subject claim); null for unauthenticated requests.</summary>
    Guid? UserId { get; }

    /// <summary>A stable display name for the caller — what gets written to the audit trail.</summary>
    string Name { get; }

    /// <summary>The customer this user represents, if the caller is a Customer; null for staff/system callers. Used to scope customer-owned data.</summary>
    Guid? CustomerId { get; }

    bool IsInRole(string role);
}
