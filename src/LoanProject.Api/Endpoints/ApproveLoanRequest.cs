namespace LoanProject.Api.Endpoints;

/// <summary>Body of POST /loans/{id}/approve.</summary>
public sealed record ApproveLoanRequest(string ApprovedBy);
