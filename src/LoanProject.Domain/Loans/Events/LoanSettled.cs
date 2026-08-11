namespace LoanProject.Domain.Loans.Events;

/// <summary>Terminal — outstanding balance reached zero.</summary>
public sealed record LoanSettled(
    Guid FinalPaymentId,
    DateTime OccurredAtUtc) : IDomainEvent;
