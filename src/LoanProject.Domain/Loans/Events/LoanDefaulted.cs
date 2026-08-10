namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// Snapshot fields (DaysOverdue, OutstandingBalance) are captured at the
/// moment of default because they matter for NPL reporting later.
/// </summary>
public sealed record LoanDefaulted(
    int DaysOverdue,
    decimal OutstandingBalance,
    DateTime OccurredAtUtc) : IDomainEvent;
