using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Result of POST /amortization/preview: the full schedule plus the headline
/// figures a borrower cares about. All money is satang-precise (2 dp), and the
/// schedule always closes at 0.00 on the final installment.
/// </summary>
public sealed record AmortizationPreviewResponse(
    decimal Principal,
    decimal AnnualRate,
    int TermMonths,
    RateType RateType,
    decimal MonthlyPayment,
    decimal TotalPaid,
    decimal TotalInterest,
    IReadOnlyList<Installment> Schedule);
