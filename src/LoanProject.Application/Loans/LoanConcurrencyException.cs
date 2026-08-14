namespace LoanProject.Application.Loans;

/// <summary>
/// Two writers raced on the same loan and this one lost: its events were
/// built on a version that is no longer the newest. The caller reloads the
/// aggregate, re-decides on fresh state, and retries.
/// </summary>
public sealed class LoanConcurrencyException : Exception
{
    public Guid LoanId { get; }
    public int ExpectedVersion { get; }

    public LoanConcurrencyException(Guid loanId, int expectedVersion, Exception innerException)
        : base(
            $"Loan '{loanId}' was modified concurrently; events built on version {expectedVersion} were rejected.",
            innerException)
    {
        LoanId = loanId;
        ExpectedVersion = expectedVersion;
    }
}
