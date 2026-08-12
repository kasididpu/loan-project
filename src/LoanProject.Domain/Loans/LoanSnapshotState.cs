namespace LoanProject.Domain.Loans;

/// <summary>
/// Memento of every non-derived Loan field — the "balance carried forward"
/// line the event store persists every 25 events. The schedule is absent on
/// purpose: it is derived state, rebuilt from the loan terms by the same
/// pure calculators that Apply uses, so storing it would only create a
/// second copy that could disagree with the first.
/// </summary>
public sealed record LoanSnapshotState(
    Guid Id,
    LoanStatus Status,
    decimal Principal,
    decimal AnnualRate,
    RateType RateType,
    int TermMonths,
    decimal OutstandingBalance,
    int NextInstallmentNo,
    int Version);
