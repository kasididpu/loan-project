using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>Body of POST /loans. The rate is looked up server-side, not supplied here.</summary>
public sealed record OriginateLoanRequest(Guid CustomerId, decimal Principal, RateType RateType, int TermMonths);
