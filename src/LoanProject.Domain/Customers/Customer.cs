namespace LoanProject.Domain.Customers;

/// <summary>
/// Conventional CRUD entity (not event-sourced, per the scoped-ES design).
/// </summary>
public sealed class Customer
{
    public Guid Id { get; }
    public string FullName { get; }
    public DateTime CreatedAtUtc { get; }

    /// <summary>KYC verification state — starts Pending, changed only through SetKycStatus.</summary>
    public KycStatus KycStatus { get; private set; }

    public Customer(Guid id, string fullName, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Customer id must not be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        Id = id;
        FullName = fullName;
        CreatedAtUtc = createdAtUtc;
        KycStatus = KycStatus.Pending;
    }

    /// <summary>Rich model: KYC status changes go through a method, not a public setter.</summary>
    public void SetKycStatus(KycStatus status) => KycStatus = status;
}
