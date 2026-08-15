using LoanProject.Infrastructure.EventStore;

namespace LoanProject.Infrastructure.Streaming;

/// <summary>
/// Publishes ledger rows to the event stream. Contract: returns only when
/// every event is acknowledged by the broker, in the given order — the
/// dispatcher advances its cursor on that promise. A failure anywhere must
/// throw so the whole batch is retried (at-least-once).
/// </summary>
public interface ILoanEventPublisher
{
    Task PublishAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken);
}
