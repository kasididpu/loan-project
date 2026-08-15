namespace LoanProject.Application.Loans;

/// <summary>
/// Disburses an approved loan. Like approval, it loads real-time from the event
/// store, never the Read DB. The amount comes from the loaded aggregate, not the
/// caller — the aggregate's rule is a single full disbursement of the approved
/// principal, so there is no money figure for the API to get wrong.
/// </summary>
public sealed class DisburseLoanHandler
{
    private readonly ILoanRepository _loans;

    public DisburseLoanHandler(ILoanRepository loans) => _loans = loans;

    public async Task HandleAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        loan.Disburse(loan.Principal, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
