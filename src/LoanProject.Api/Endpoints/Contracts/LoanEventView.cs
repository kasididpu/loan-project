using System.Text.Json;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// One event in the GET /loans/{id}/events response. Data is the stored payload
/// inlined as JSON (not an escaped string), so the audit trail reads cleanly.
/// </summary>
public sealed record LoanEventView(
    int Version,
    string EventType,
    DateTime OccurredAtUtc,
    JsonElement Data);
