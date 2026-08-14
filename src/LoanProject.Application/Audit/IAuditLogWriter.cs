namespace LoanProject.Application.Audit;

/// <summary>
/// Operational audit trail — the night watchman's book, not the ledger.
/// The event store remains the only source of truth for the Loan aggregate;
/// audit entries exist so operations can ask "who did what, when" across
/// the whole system without ever touching the ledger. Losing this log hurts
/// forensics, never correctness.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>All entries for one entity, oldest first.</summary>
    Task<IReadOnlyList<AuditEntry>> ListByEntityAsync(
        string entityType, string entityId, CancellationToken cancellationToken);
}
