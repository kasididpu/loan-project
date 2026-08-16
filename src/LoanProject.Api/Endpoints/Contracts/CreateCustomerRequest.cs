namespace LoanProject.Api.Endpoints;

/// <summary>
/// Body of POST /customers. NationalId and BankAccountNumber are PII — stored
/// encrypted at rest and returned masked; only sent in the clear on creation.
/// </summary>
public sealed record CreateCustomerRequest(string FullName, string NationalId, string BankAccountNumber);
