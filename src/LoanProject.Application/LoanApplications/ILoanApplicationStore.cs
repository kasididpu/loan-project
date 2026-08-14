namespace LoanProject.Application.LoanApplications;

public interface ILoanApplicationStore
{
    /// <summary>Upserts by id — resubmitting an application replaces it.</summary>
    Task SaveAsync(LoanApplicationDocument document, CancellationToken cancellationToken);

    Task<LoanApplicationDocument?> FindAsync(Guid id, CancellationToken cancellationToken);
}
