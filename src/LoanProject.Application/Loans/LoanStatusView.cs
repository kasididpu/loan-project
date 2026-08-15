namespace LoanProject.Application.Loans;

/// <summary>
/// Read-side view of a loan, served from the eventually-consistent Read DB.
/// Version echoes how far the projection has caught up, so a caller can tell a
/// fresh write has not landed yet.
/// </summary>
public sealed record LoanStatusView(
    Guid LoanId,
    Guid CustomerId,
    string Status,
    decimal Principal,
    decimal AnnualRate,
    string RateType,
    int TermMonths,
    decimal OutstandingBalance,
    int NextInstallmentNo,
    DateTime? NextDueDateUtc,
    decimal TotalPaid,
    int InstallmentsPaid,
    int Version);
