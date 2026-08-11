namespace LoanProject.Domain.Customers;

/// <summary>
/// Conventional CRUD entity (not event-sourced, per the scoped-ES design).
/// KYC fields arrive in a later phase.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; }
    public string FullName { get; }
    public DateTime CreatedAtUtc { get; }

    public Customer(Guid id, string fullName, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Customer id must not be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        Id = id;
        FullName = fullName;
        CreatedAtUtc = createdAtUtc;
    }
}
