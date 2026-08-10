namespace LoanProject.Domain.Loans.Events;

public sealed record LoanApproved(
    string ApprovedBy,
    DateTime OccurredAtUtc) : IDomainEvent;
