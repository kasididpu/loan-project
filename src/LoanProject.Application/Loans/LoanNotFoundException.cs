namespace LoanProject.Application.Loans;

/// <summary>Raised when a command targets a loan id that has no event stream.</summary>
public sealed class LoanNotFoundException : Exception
{
    public LoanNotFoundException(Guid loanId)
        : base($"Loan '{loanId}' was not found.") => LoanId = loanId;

    public Guid LoanId { get; }
}
