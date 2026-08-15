using System.Text.Json;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.ReadModel;
using LoanProject.Infrastructure.Streaming;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Shared helpers for read-model tests: turn an aggregate's uncommitted events
/// into the exact wire envelopes the projector consumes (same serializer as
/// production), and drive them through the projection one fresh context at a
/// time — mirroring the one-scope-per-message shape of the real projector.
/// </summary>
internal static class ReadModelTesting
{
    public static IReadOnlyList<LoanEventEnvelope> Envelopes(Loan loan)
    {
        var envelopes = new List<LoanEventEnvelope>();
        var version = 0;
        long sequence = 0;
        foreach (var domainEvent in loan.UncommittedEvents)
        {
            version++;
            sequence++;
            var (eventType, json) = LoanEventSerializer.Serialize(domainEvent);
            var data = JsonDocument.Parse(json).RootElement.Clone();
            envelopes.Add(new LoanEventEnvelope(sequence, loan.Id, version, eventType, data, domainEvent.OccurredAtUtc));
        }

        return envelopes;
    }

    public static async Task ProjectAllAsync(IEnumerable<LoanEventEnvelope> envelopes)
    {
        foreach (var envelope in envelopes)
        {
            await using var db = TestReadDatabase.CreateContext();
            await new LoanReadModelProjection(db).ProjectAsync(envelope, CancellationToken.None);
        }
    }
}
