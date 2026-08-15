namespace LoanProject.Application.Settlement;

/// <summary>Outcome of one end-of-day settlement run, for logs and the audit trail.</summary>
public sealed record SettlementResult(
    DateOnly BusinessDate,
    int LoanCount,
    decimal TotalCollected);
