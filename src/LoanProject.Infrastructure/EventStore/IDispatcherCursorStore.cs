namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// The dispatcher's bookmark: the highest EventStore.Sequence already
/// published to Redpanda. Advancing only after a confirmed publish is what
/// makes delivery at-least-once — a crash between publish and advance means
/// re-publish, never loss.
/// </summary>
public interface IDispatcherCursorStore
{
    /// <summary>Zero when no cursor row exists yet — publish from the beginning.</summary>
    Task<long> GetLastSequenceAsync(CancellationToken cancellationToken);

    Task AdvanceAsync(long lastSequence, CancellationToken cancellationToken);
}
