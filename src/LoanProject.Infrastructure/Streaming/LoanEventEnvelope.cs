using System.Text.Json;

namespace LoanProject.Infrastructure.Streaming;

/// <summary>
/// Wire shape of one loan event on the loan-events topic. EventData rides
/// along as raw JSON (not a re-encoded string), so consumers read one clean
/// document; (AggregateId, Version) is their dedupe key. Property casing
/// matches the stored payloads (PascalCase) — one contract end to end.
/// </summary>
public sealed record LoanEventEnvelope(
    long Sequence,
    Guid AggregateId,
    int Version,
    string EventType,
    JsonElement EventData,
    DateTime OccurredAtUtc);
