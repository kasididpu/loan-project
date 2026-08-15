using LoanProject.Domain.Customers;

namespace LoanProject.Application.Customers;

/// <summary>A loan approval was attempted for a customer who is not KYC-verified (Phase 7).</summary>
public sealed class KycNotVerifiedException : Exception
{
    public KycNotVerifiedException(Guid customerId, KycStatus? actual)
        : base($"Customer '{customerId}' is not KYC-verified (status: {actual?.ToString() ?? "unknown"}); the loan cannot be approved.")
    {
        CustomerId = customerId;
        ActualStatus = actual;
    }

    public Guid CustomerId { get; }
    public KycStatus? ActualStatus { get; }
}
