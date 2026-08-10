namespace LoanProject.Domain.Loans.Events;

/// <summary>Money goes out — the debt (outstanding balance) starts here.</summary>
public sealed record LoanDisbursed(
    decimal DisbursedAmount,
    DateTime OccurredAtUtc) : IDomainEvent;
