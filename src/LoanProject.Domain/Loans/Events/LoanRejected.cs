namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// Terminal — no further events are valid after rejection. RejectedByUserId is
/// the officer's immutable identity for audit; RejectedBy keeps the display name.
/// Events stored before this field existed deserialize the id as Guid.Empty
/// (replay-safe).
/// </summary>
public sealed record LoanRejected(
    Guid RejectedByUserId,
    string RejectedBy,
    string Reason,
    DateTime OccurredAtUtc) : IDomainEvent;
