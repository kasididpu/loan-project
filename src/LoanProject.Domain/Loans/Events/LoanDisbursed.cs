namespace LoanProject.Domain.Loans.Events;

/// <summary>
/// Money goes out — the debt (outstanding balance) starts here. DisbursedByUserId
/// / DisbursedBy identify the officer who released the funds (id for a stable
/// audit reference, name for readability). Events stored before these fields
/// existed deserialize them as Guid.Empty / null — replay stays safe because no
/// state transition branches on them.
/// </summary>
public sealed record LoanDisbursed(
    decimal DisbursedAmount,
    Guid DisbursedByUserId,
    string DisbursedBy,
    DateTime OccurredAtUtc) : IDomainEvent;
