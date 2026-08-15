namespace LoanProject.Application.Loans;

/// <summary>
/// Approves a loan. The aggregate is loaded straight from the event store — the
/// real-time source of truth — never the read model: an approval decision must
/// see the loan's current state, not an eventually-consistent projection that
/// may lag. This is the deliberate "bypass the Read DB" point Phase 6 calls out.
/// </summary>
public sealed class ApproveLoanHandler
{
    private readonly ILoanRepository _loans;

    public ApproveLoanHandler(ILoanRepository loans) => _loans = loans;

    public async Task HandleAsync(Guid loanId, string approvedBy, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        loan.Approve(approvedBy, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
