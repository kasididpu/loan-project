using LoanProject.Domain.Customers;

namespace LoanProject.Application.Customers;

public interface ICustomerRepository
{
    /// <summary>
    /// Registers the customer with the unit of work — synchronous on purpose:
    /// nothing touches the database until <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    void Add(Customer customer);

    Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken);
}
