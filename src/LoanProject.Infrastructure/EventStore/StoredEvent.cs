namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// One EventStore row exactly as persisted — the dispatcher moves rows, it
/// never deserializes domain events (that stays a consumer concern).
/// </summary>
public sealed record StoredEvent(
    long Sequence,
    Guid AggregateId,
    int Version,
    string EventType,
    string EventData,
    DateTime OccurredAtUtc);
