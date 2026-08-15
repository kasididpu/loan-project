using LoanProject.Domain.Loans;

namespace LoanProject.Application.Rates;

/// <summary>
/// Annual nominal interest rate for a product shape (rate type + term).
/// In a real lender this answer comes from a pricing service or rate sheet
/// database — slow enough to be worth caching, which is the point of the
/// Redis decorator in front of it (phase 5).
/// </summary>
public interface IInterestRateLookup
{
    /// <summary>Annual rate as a fraction (0.16m = 16% per year).</summary>
    Task<decimal> GetAnnualRateAsync(RateType rateType, int termMonths, CancellationToken cancellationToken);
}
