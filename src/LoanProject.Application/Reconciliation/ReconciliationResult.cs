namespace LoanProject.Application.Reconciliation;

/// <summary>Outcome of one reconciliation run, for logs and the audit trail.</summary>
public sealed record ReconciliationResult(
    int StripeEventCount,
    int IgnoredCount,
    IReadOnlyList<string> MissingEventIds)
{
    public bool IsClean => MissingEventIds.Count == 0;
}
