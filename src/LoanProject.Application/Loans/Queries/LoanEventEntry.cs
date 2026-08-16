namespace LoanProject.Application.Loans;

/// <summary>One row of a loan's event stream, exactly as stored in the ledger.</summary>
public sealed record LoanEventEntry(
    int Version,
    string EventType,
    DateTime OccurredAtUtc,
    string EventDataJson);
