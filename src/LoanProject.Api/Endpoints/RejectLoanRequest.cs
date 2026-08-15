namespace LoanProject.Api.Endpoints;

/// <summary>Body of POST /loans/{id}/reject.</summary>
public sealed record RejectLoanRequest(string RejectedBy, string Reason);
