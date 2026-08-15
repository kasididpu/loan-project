namespace LoanProject.Infrastructure.Messaging;

/// <summary>
/// Consumer-side idempotency for payment notices: at-least-once delivery
/// means duplicates are normal, and notifying a customer twice is not.
/// In-memory on purpose — a restart forgetting seen ids only risks one
/// repeat notification, which the at-least-once model already allows.
/// </summary>
public sealed class PaymentNotificationDeduplicator
{
    // Bounded so an unattended consumer cannot grow without limit; the queue
    // for this system holds nowhere near this many in-flight notices.
    private const int MaxTrackedIds = 10_000;

    private readonly object _gate = new();
    private readonly HashSet<string> _seenIds = new();
    private readonly Queue<string> _evictionOrder = new();

    /// <summary>True the first time an id is seen; false for every repeat.</summary>
    public bool TryRegister(string stripeEventId)
    {
        lock (_gate)
        {
            if (!_seenIds.Add(stripeEventId))
                return false;

            _evictionOrder.Enqueue(stripeEventId);
            if (_evictionOrder.Count > MaxTrackedIds)
                _seenIds.Remove(_evictionOrder.Dequeue());
            return true;
        }
    }
}
