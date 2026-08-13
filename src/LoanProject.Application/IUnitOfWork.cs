namespace LoanProject.Application;

/// <summary>
/// Commits everything the repositories registered as one transaction —
/// all of it lands, or none of it does. Returns the number of rows written.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
