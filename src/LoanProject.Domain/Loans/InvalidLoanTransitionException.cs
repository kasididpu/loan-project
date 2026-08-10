namespace LoanProject.Domain.Loans;

/// <summary>
/// Thrown when a command is not allowed in the loan's current status.
/// Every throw site must be covered by a unit test (project rule).
/// </summary>
public sealed class InvalidLoanTransitionException : Exception
{
    public LoanStatus CurrentStatus { get; }
    public string AttemptedAction { get; }

    public InvalidLoanTransitionException(LoanStatus currentStatus, string attemptedAction)
        : base($"Cannot {attemptedAction} a loan in status '{currentStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }
}
