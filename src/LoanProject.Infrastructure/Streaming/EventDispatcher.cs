using LoanProject.Infrastructure.EventStore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanProject.Infrastructure.Streaming;

/// <summary>
/// The event-store-as-outbox dispatcher (phase 5): reads ledger rows past
/// the cursor, publishes them to Redpanda, then advances the cursor. That
/// order is the dual-write fix — the ledger commit and the publish are two
/// steps, but a crash between them only ever means re-publish, never a
/// stored event that no consumer hears about. Must run as a single active
/// instance (the singleton cursor row backs this mechanically).
/// </summary>
public sealed class EventDispatcher(
    IEventStoreReader eventStoreReader,
    IDispatcherCursorStore cursorStore,
    ILoanEventPublisher publisher,
    ILogger<EventDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Event dispatcher started.");
        var backoff = InitialBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await DispatchPendingAsync(stoppingToken);
                backoff = InitialBackoff;

                // A full batch suggests more is waiting — drain immediately;
                // otherwise sleep one poll interval.
                if (published < BatchSize)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Broker or database down: keep the cursor where it is and
                // retry the same batch — this is the at-least-once promise.
                logger.LogWarning(exception,
                    "Event dispatch failed; retrying in {Backoff}.", backoff);
                try
                {
                    await Task.Delay(backoff, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
            }
        }

        logger.LogInformation("Event dispatcher stopped.");
    }

    /// <summary>One pass: read past the cursor, publish, advance. Returns the count published.</summary>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var lastSequence = await cursorStore.GetLastSequenceAsync(cancellationToken);
        var batch = await eventStoreReader.ReadBatchAsync(lastSequence, BatchSize, cancellationToken);
        if (batch.Count == 0)
            return 0;

        await publisher.PublishAsync(batch, cancellationToken);

        // Advance only after every event in the batch is acknowledged. If
        // this line never runs, the next pass re-reads the same rows.
        await cursorStore.AdvanceAsync(batch[^1].Sequence, cancellationToken);

        logger.LogInformation(
            "Dispatched {Count} event(s) up to sequence {LastSequence}.",
            batch.Count, batch[^1].Sequence);
        return batch.Count;
    }
}
