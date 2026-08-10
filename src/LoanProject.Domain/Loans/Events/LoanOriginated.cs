namespace LoanProject.Domain.Loans.Events;

/// <summary>Starts the stream — always version 1.</summary>
public sealed record LoanOriginated(
    Guid LoanId,
    Guid CustomerId,
    decimal Principal,
    decimal AnnualRate,
    RateType RateType,
    int TermMonths,
    DateTime OccurredAtUtc) : IDomainEvent;
