namespace LoanProject.Application.Customers;

/// <summary>Raised when a command targets a customer id that does not exist.</summary>
public sealed class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(Guid customerId)
        : base($"Customer '{customerId}' was not found.") => CustomerId = customerId;

    public Guid CustomerId { get; }
}
