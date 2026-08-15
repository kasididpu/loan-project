namespace LoanProject.Application.Loans;

/// <summary>Query side: reads a single loan's current view from the Read DB.</summary>
public interface ILoanStatusQuery
{
    Task<LoanStatusView?> GetAsync(Guid loanId, CancellationToken cancellationToken);
}
