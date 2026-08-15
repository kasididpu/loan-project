using LoanProject.Domain.Customers;

namespace LoanProject.Application.Customers;

/// <summary>
/// Sets a customer's KYC status (Phase 7) — a simulated compliance action, no
/// external verification system. The tracked customer is mutated and saved
/// through the unit of work.
/// </summary>
public sealed class SetKycStatusHandler
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public SetKycStatusHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(Guid customerId, KycStatus status, CancellationToken cancellationToken)
    {
        var customer = await _customers.FindAsync(customerId, cancellationToken)
            ?? throw new CustomerNotFoundException(customerId);

        customer.SetKycStatus(status);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
