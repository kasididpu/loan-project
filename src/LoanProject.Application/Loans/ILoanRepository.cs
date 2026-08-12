using LoanProject.Domain.Loans;

namespace LoanProject.Application.Loans;

/// <summary>
/// Port for Loan persistence. The only implementation is the event store
/// repository — the Loan aggregate never touches an ORM mapping.
/// </summary>
public interface ILoanRepository
{
    /// <summary>Returns null when no stream exists for the id.</summary>
    Task<Loan?> LoadAsync(Guid loanId, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the aggregate's uncommitted events with optimistic
    /// concurrency; throws <see cref="LoanConcurrencyException"/> when
    /// another writer got there first (reload and retry).
    /// </summary>
    Task SaveAsync(Loan loan, CancellationToken cancellationToken);
}
