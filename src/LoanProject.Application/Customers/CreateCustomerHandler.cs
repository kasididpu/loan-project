using LoanProject.Domain.Customers;

namespace LoanProject.Application.Customers;

/// <summary>
/// Onboards a customer with their identity documents. Plain CRUD (customers are
/// not event-sourced); the PII it receives is encrypted at rest by the
/// persistence layer's value converter, transparently to this handler.
/// </summary>
public sealed class CreateCustomerHandler
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Customer> HandleAsync(
        string fullName, string nationalId, string bankAccountNumber, CancellationToken cancellationToken)
    {
        var customer = new Customer(Guid.NewGuid(), fullName, DateTime.UtcNow);
        customer.SetIdentityDocuments(nationalId, bankAccountNumber);

        _customers.Add(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return customer;
    }
}
