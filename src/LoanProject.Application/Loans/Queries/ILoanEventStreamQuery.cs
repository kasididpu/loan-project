namespace LoanProject.Application.Loans;

/// <summary>
/// Reads a loan's full append-only event stream (its audit trail) straight from
/// the ledger — every state transition in order, for a back-office view.
/// </summary>
public interface ILoanEventStreamQuery
{
    Task<IReadOnlyList<LoanEventEntry>> GetAsync(Guid loanId, CancellationToken cancellationToken);
}
