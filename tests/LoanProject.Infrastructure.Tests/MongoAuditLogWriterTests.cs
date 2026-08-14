using LoanProject.Application.Audit;
using LoanProject.Infrastructure.Mongo;

namespace LoanProject.Infrastructure.Tests;

public class MongoAuditLogWriterTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WriteAsync_ThenListByEntity_ReturnsOnlyThatEntitysEntriesOldestFirst()
    {
        var writer = new MongoAuditLogWriter(TestMongo.Database);
        var loanId = Guid.NewGuid().ToString();
        var otherLoanId = Guid.NewGuid().ToString();

        var approved = new AuditEntry("Loan", loanId, "StatusChanged", "officer-1", Now,
            new Dictionary<string, object?> { ["from"] = "Originated", ["to"] = "Approved" });
        var disbursed = new AuditEntry("Loan", loanId, "StatusChanged", "system", Now.AddMinutes(5),
            new Dictionary<string, object?> { ["from"] = "Approved", ["to"] = "Active", ["installments"] = 12 });
        var foreign = new AuditEntry("Loan", otherLoanId, "StatusChanged", "officer-2", Now,
            new Dictionary<string, object?> { ["from"] = "Originated", ["to"] = "Rejected" });

        // Inserted newest-first on purpose — the read side must sort by time.
        await writer.WriteAsync(disbursed, CancellationToken.None);
        await writer.WriteAsync(approved, CancellationToken.None);
        await writer.WriteAsync(foreign, CancellationToken.None);

        var entries = await writer.ListByEntityAsync("Loan", loanId, CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Approved", entries[0].Details["to"]);       // oldest first
        Assert.Equal("Active", entries[1].Details["to"]);
        Assert.Equal("system", entries[1].Actor);
        Assert.Equal(12, entries[1].Details["installments"]);     // int survives the round trip
        Assert.Equal(Now.AddMinutes(5), entries[1].OccurredAtUtc);
    }
}
