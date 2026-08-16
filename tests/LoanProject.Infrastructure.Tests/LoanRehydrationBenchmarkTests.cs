using System.Diagnostics;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Phase 10 measurement that feeds the snapshot-interval ADR: rehydrating a
/// long-lived loan WITH a snapshot (replay only the tail past it) versus WITHOUT
/// one (replay the whole ledger). The timings are printed for the load-test
/// report; the assertion is correctness — both paths must rebuild the identical
/// aggregate, which is the guarantee a snapshot cache has to keep.
///
/// Run with output visible:
///   dotnet test --filter FullyQualifiedName~Rehydration -l "console;verbosity=detailed"
/// </summary>
public class LoanRehydrationBenchmarkTests
{
    private const int Iterations = 50;

    private readonly ITestOutputHelper _output;

    public LoanRehydrationBenchmarkTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Rehydrate_ManyEventLoan_SnapshotMatchesFullReplay_AndReportsTiming()
    {
        var repository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loanId = Guid.NewGuid();
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // A 60-installment loan paid to term: originate + approve + disburse + 60
        // payments = 63 events, crossing the snapshot interval (25) twice.
        var loan = Loan.Originate(loanId, Guid.NewGuid(), 600_000m, 0.12m, RateType.Effective, 60, date);
        loan.Approve(Guid.NewGuid(), "bench-officer", date);
        loan.Disburse(600_000m, Guid.NewGuid(), "bench-officer", date);
        for (var installmentNo = 1; installmentNo <= 60; installmentNo++)
            loan.ReceivePayment(
                Guid.NewGuid(), loan.Schedule![installmentNo - 1].Payment, installmentNo,
                $"evt_bench_{installmentNo}", date.AddMonths(installmentNo));

        await repository.SaveAsync(loan, CancellationToken.None);
        var eventCount = loan.Version;

        // With snapshot (repo default: latest snapshot + replay only the tail).
        var withSnapshot = await MeasureAsync(repository, loanId);

        // Drop the snapshot cache — NOT the ledger — to force a full replay.
        await DeleteSnapshotAsync(loanId);
        var fullReplay = await MeasureAsync(repository, loanId);

        _output.WriteLine($"Events in stream      : {eventCount}");
        _output.WriteLine($"Rehydrate w/ snapshot : {withSnapshot.AvgMs:F3} ms avg over {Iterations}");
        _output.WriteLine($"Rehydrate full replay : {fullReplay.AvgMs:F3} ms avg over {Iterations}");
        _output.WriteLine($"Snapshot speedup      : {fullReplay.AvgMs / withSnapshot.AvgMs:F2}x");

        // The measurement is informational; correctness is the guarantee: the two
        // paths must produce the same aggregate.
        Assert.Equal(eventCount, withSnapshot.Version);
        Assert.Equal(withSnapshot.Version, fullReplay.Version);
        Assert.Equal(withSnapshot.Status, fullReplay.Status);
    }

    private static async Task<(double AvgMs, int Version, LoanStatus Status)> MeasureAsync(
        LoanEventStoreRepository repository, Guid loanId)
    {
        var loaded = await repository.LoadAsync(loanId, CancellationToken.None); // warm up JIT + plan cache

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            loaded = await repository.LoadAsync(loanId, CancellationToken.None);
        stopwatch.Stop();

        return (stopwatch.Elapsed.TotalMilliseconds / Iterations, loaded!.Version, loaded.Status);
    }

    private static async Task DeleteSnapshotAsync(Guid loanId)
    {
        // The snapshot is a rebuildable cache, not part of the append-only ledger,
        // so clearing it is legitimate (the repository itself overwrites it).
        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "DELETE FROM LoanSnapshot WHERE AggregateId = @AggregateId", connection);
        command.Parameters.AddWithValue("@AggregateId", loanId);
        await command.ExecuteNonQueryAsync();
    }
}
