namespace LoanProject.Api.Endpoints;

/// <summary>
/// Body of POST /loans/{id}/reject. Only the reason comes from the caller — the
/// rejecting officer is taken from the authenticated token (Phase 8), not the body.
/// </summary>
public sealed record RejectLoanRequest(string Reason);
