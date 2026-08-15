namespace LoanProject.Domain.Customers;

/// <summary>KYC verification state of a customer (Phase 7). New customers start Pending.</summary>
public enum KycStatus
{
    Pending,
    Verified,
    Rejected,
}
