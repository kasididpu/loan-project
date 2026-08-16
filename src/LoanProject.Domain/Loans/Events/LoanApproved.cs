namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// Loan approved. ApprovedByUserId is the officer's immutable identity (the token
/// subject) — the trustworthy audit reference; ApprovedBy keeps the display name
/// for readability. Events stored before this field existed deserialize the id as
/// Guid.Empty (replay-safe), since JSON is matched by name, not position.
/// </summary>
public sealed record LoanApproved(
    Guid ApprovedByUserId,
    string ApprovedBy,
    DateTime OccurredAtUtc) : IDomainEvent;
