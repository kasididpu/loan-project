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

    /// <summary>Thai national ID (13 digits). PII — persisted encrypted at rest (Phase 8).</summary>
    public string? NationalId { get; private set; }

    /// <summary>Bank account number used for disbursement. PII — persisted encrypted at rest (Phase 8).</summary>
    public string? BankAccountNumber { get; private set; }

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

    /// <summary>
    /// Attaches the identity documents collected at onboarding. Kept out of the
    /// constructor so paths that create a customer before documents arrive still
    /// compile; validation lives here, so a set value is always well-formed. How
    /// the values are protected at rest (encryption) is an Infrastructure concern —
    /// the domain only holds the cleartext.
    /// </summary>
    public void SetIdentityDocuments(string nationalId, string bankAccountNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
            throw new ArgumentException("National ID is required.", nameof(nationalId));
        if (string.IsNullOrWhiteSpace(bankAccountNumber))
            throw new ArgumentException("Bank account number is required.", nameof(bankAccountNumber));

        NationalId = nationalId;
        BankAccountNumber = bankAccountNumber;
    }
}
