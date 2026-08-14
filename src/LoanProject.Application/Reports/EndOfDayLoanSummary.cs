namespace LoanProject.Application.Reports;

/// <summary>One loan's collections for a single day, as reported by the database.</summary>
public sealed record EndOfDayLoanSummary(
    Guid LoanId,
    int PaymentsCount,
    decimal TotalCollected,
    DateTime LastPaymentAtUtc);
