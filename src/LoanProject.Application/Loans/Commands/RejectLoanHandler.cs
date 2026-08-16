using LoanProject.Application.Auth;

namespace LoanProject.Application.Loans;

/// <summary>
/// Rejects a loan still awaiting a decision. Real-time load from the event store.
/// The rejecting officer is the authenticated caller (Phase 8); only the reason
/// still comes from the request body.
/// </summary>
public sealed class RejectLoanHandler
{
    private readonly ILoanRepository _loans;
    private readonly ICurrentUser _currentUser;

    public RejectLoanHandler(ILoanRepository loans, ICurrentUser currentUser)
    {
        _loans = loans;
        _currentUser = currentUser;
    }

    public async Task HandleAsync(Guid loanId, string reason, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        var officerId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated caller has no subject id.");
        loan.Reject(officerId, _currentUser.Name, reason, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
