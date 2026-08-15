namespace LoanProject.Application.Loans;

/// <summary>Rejects a loan still awaiting a decision. Real-time load from the event store.</summary>
public sealed class RejectLoanHandler
{
    private readonly ILoanRepository _loans;

    public RejectLoanHandler(ILoanRepository loans) => _loans = loans;

    public async Task HandleAsync(Guid loanId, string rejectedBy, string reason, CancellationToken cancellationToken)
    {
        var loan = await _loans.LoadAsync(loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        loan.Reject(rejectedBy, reason, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
    }
}
