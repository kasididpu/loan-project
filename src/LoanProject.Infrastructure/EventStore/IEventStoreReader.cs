namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// The dispatcher's read window into the ledger: everything past a global
/// sequence, in write order. Interface exists so the dispatch loop can be
/// tested without a database.
/// </summary>
public interface IEventStoreReader
{
    Task<IReadOnlyList<StoredEvent>> ReadBatchAsync(
        long afterSequence, int batchSize, CancellationToken cancellationToken);
}
