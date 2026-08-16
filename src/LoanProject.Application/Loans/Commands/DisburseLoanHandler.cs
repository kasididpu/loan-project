using LoanProject.Application.Auth;

namespace LoanProject.Application.Loans;

/// <summary>
/// Disburses an approved loan. Like approval, it loads real-time from the event
/// store, never the Read DB. The amount comes from the loaded aggregate, not the
/// caller — the aggregate's rule is a single full disbursement of the approved
/// principal, so there is no money figure for the API to get wrong. The officer
/// releasing the funds is the authenticated caller (Phase 8), recorded on the
/// LoanDisbursed event for the audit trail — this is a payment-path action.
/// </summary>
public sealed class DisburseLoanHandler
{
    private readonly ILoanRepository _loans;
    private readonly ICurrentUser _currentUser;

    public DisburseLoanHandler(ILoanRepository loans, ICurrentUser currentUser)
    {
        _loans = loans;
        _currentUser = currentUser;
    }

    public async Task HandleAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        var officerId = _currentUser.UserId
            ?? throw new InvalidOperationException("Authenticated caller has no subject id.");
        loan.Disburse(loan.Principal, officerId, _currentUser.Name, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
