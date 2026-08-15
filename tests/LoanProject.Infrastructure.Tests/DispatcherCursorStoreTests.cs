using LoanProject.Infrastructure.EventStore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Cursor store against the real singleton DispatcherCursor row. The cursor
/// is shared state (the running app's dispatcher uses the same row), so
/// every test restores the original value — moving it backward is harmless
/// (at-least-once re-publish), moving it forward would lose events.
/// </summary>
public class DispatcherCursorStoreTests
{
    [Fact]
    public async Task AdvanceAsync_NewValue_SurvivesANewStoreInstance()
    {
        var store = new DispatcherCursorStore(TestDatabase.ConnectionString);
        var original = await store.GetLastSequenceAsync(CancellationToken.None);
        try
        {
            await store.AdvanceAsync(original + 1, CancellationToken.None);

            // A fresh instance is the "restarted dispatcher": the bookmark
            // must come back from the database, not from memory.
            var restarted = new DispatcherCursorStore(TestDatabase.ConnectionString);
            Assert.Equal(original + 1, await restarted.GetLastSequenceAsync(CancellationToken.None));
        }
        finally
        {
            await store.AdvanceAsync(original, CancellationToken.None);
        }
    }

    [Fact]
    public async Task AdvanceAsync_CalledTwice_KeepsTheLatestValue()
    {
        var store = new DispatcherCursorStore(TestDatabase.ConnectionString);
        var original = await store.GetLastSequenceAsync(CancellationToken.None);
        try
        {
            await store.AdvanceAsync(original + 1, CancellationToken.None);
            await store.AdvanceAsync(original + 2, CancellationToken.None);

            Assert.Equal(original + 2, await store.GetLastSequenceAsync(CancellationToken.None));
        }
        finally
        {
            await store.AdvanceAsync(original, CancellationToken.None);
        }
    }
}
