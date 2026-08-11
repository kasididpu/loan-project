namespace LoanProject.Domain.Loans.Events;

/// <summary>Terminal — no further events are valid after rejection.</summary>
public sealed record LoanRejected(
    string RejectedBy,
    string Reason,
    DateTime OccurredAtUtc) : IDomainEvent;
