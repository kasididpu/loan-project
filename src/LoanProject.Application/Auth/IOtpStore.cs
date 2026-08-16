namespace LoanProject.Application.Auth;

/// <summary>
/// Short-lived store for one-time passcodes during MFA. Backed by Redis with a
/// TTL, so an unused code expires on its own. Codes are single-use: a successful
/// validation consumes the entry so it cannot be replayed.
/// </summary>
public interface IOtpStore
{
    Task StoreAsync(Guid subjectId, string code, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>True if the code matches the stored one; consumes it on success.</summary>
    Task<bool> ValidateAndConsumeAsync(Guid subjectId, string code, CancellationToken cancellationToken);
}
