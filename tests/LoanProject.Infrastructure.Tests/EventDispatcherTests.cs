using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The dispatch loop's contract, tested with in-memory ports: publish
/// everything past the cursor, advance only after the publish succeeds, and
/// re-deliver the same batch after a failure (at-least-once).
/// </summary>
public class EventDispatcherTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static StoredEvent Stored(long sequence, Guid aggregateId, int version) =>
        new(sequence, aggregateId, version, "PaymentReceived", "{}", Now);

    private static EventDispatcher CreateDispatcher(
        FakeReader reader, FakeCursor cursor, FakePublisher publisher) =>
        new(reader, cursor, publisher, NullLogger<EventDispatcher>.Instance);

    [Fact]
    public async Task DispatchPendingAsync_EventsPastCursor_PublishesAndAdvances()
    {
        var aggregateId = Guid.NewGuid();
        var reader = new FakeReader(Stored(11, aggregateId, 1), Stored(12, aggregateId, 2));
        var cursor = new FakeCursor { LastSequence = 10 };
        var publisher = new FakePublisher();

        var published = await CreateDispatcher(reader, cursor, publisher)
            .DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(2, published);
        Assert.Equal(new[] { 11L, 12L }, publisher.PublishedSequences);
        Assert.Equal(12, cursor.LastSequence);
    }

    [Fact]
    public async Task DispatchPendingAsync_NothingPastCursor_PublishesNothing()
    {
        var reader = new FakeReader();
        var cursor = new FakeCursor { LastSequence = 42 };
        var publisher = new FakePublisher();

        var published = await CreateDispatcher(reader, cursor, publisher)
            .DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(0, published);
        Assert.Empty(publisher.PublishedSequences);
        Assert.Equal(42, cursor.LastSequence);
    }

    [Fact]
    public async Task DispatchPendingAsync_PublishFails_CursorDoesNotMove()
    {
        var reader = new FakeReader(Stored(11, Guid.NewGuid(), 1));
        var cursor = new FakeCursor { LastSequence = 10 };
        var publisher = new FakePublisher { FailNext = true };
        var dispatcher = CreateDispatcher(reader, cursor, publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchPendingAsync(CancellationToken.None));

        // The broker never acknowledged, so the bookmark stays put...
        Assert.Equal(10, cursor.LastSequence);

        // ...and the next pass re-delivers the same event: at-least-once.
        var published = await dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Equal(1, published);
        Assert.Equal(new[] { 11L }, publisher.PublishedSequences);
        Assert.Equal(11, cursor.LastSequence);
    }

    private sealed class FakeReader(params StoredEvent[] events) : IEventStoreReader
    {
        public Task<IReadOnlyList<StoredEvent>> ReadBatchAsync(
            long afterSequence, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredEvent>>(
                events.Where(storedEvent => storedEvent.Sequence > afterSequence)
                    .Take(batchSize)
                    .ToList());
    }

    private sealed class FakeCursor : IDispatcherCursorStore
    {
        public long LastSequence { get; set; }

        public Task<long> GetLastSequenceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LastSequence);

        public Task AdvanceAsync(long lastSequence, CancellationToken cancellationToken)
        {
            LastSequence = lastSequence;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublisher : ILoanEventPublisher
    {
        public List<long> PublishedSequences { get; } = [];
        public bool FailNext { get; set; }

        public Task PublishAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("Broker unavailable.");
            }

            PublishedSequences.AddRange(events.Select(storedEvent => storedEvent.Sequence));
            return Task.CompletedTask;
        }
    }
}
