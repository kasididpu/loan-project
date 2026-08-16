using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Body of POST /amortization/preview. A stateless calculator: the rate is
/// supplied directly (no lookup, no database), so the endpoint is pure CPU —
/// which makes it a clean load-test target for the money-calc hot path.
/// RateType selects the schedule shape: Effective = reducing balance, Flat.
/// </summary>
public sealed record AmortizationPreviewRequest(
    decimal Principal,
    decimal AnnualRate,
    int TermMonths,
    RateType RateType);
