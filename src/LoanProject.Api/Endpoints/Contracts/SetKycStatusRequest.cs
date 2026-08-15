using LoanProject.Domain.Customers;

namespace LoanProject.Api.Endpoints;

/// <summary>Body of PUT /customers/{id}/kyc. Status arrives as a name, e.g. "Verified".</summary>
public sealed record SetKycStatusRequest(KycStatus Status);
