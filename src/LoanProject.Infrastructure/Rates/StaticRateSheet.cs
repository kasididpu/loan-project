using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;

namespace LoanProject.Infrastructure.Rates;

/// <summary>
/// The rate "source of record" — in-code stand-in for the pricing database
/// or service a real lender would query. Values are arbitrary dev/test
/// pricing, not business advice; the artificial delay makes the source
/// measurably slower than the Redis cache in front of it, so cache hits
/// are visible in demos and load tests.
/// </summary>
public sealed class StaticRateSheet : IInterestRateLookup
{
    private static readonly TimeSpan SimulatedSourceLatency = TimeSpan.FromMilliseconds(150);

    public async Task<decimal> GetAnnualRateAsync(
        RateType rateType, int termMonths, CancellationToken cancellationToken)
    {
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), termMonths, "Term must be positive.");

        await Task.Delay(SimulatedSourceLatency, cancellationToken);

        // Flat quotes one rate regardless of term (hire-purchase style);
        // effective tiers up with term length — longer exposure, higher rate.
        return rateType switch
        {
            RateType.Flat => 0.12m,
            RateType.Effective when termMonths <= 12 => 0.16m,
            RateType.Effective when termMonths <= 36 => 0.18m,
            RateType.Effective => 0.20m,
            _ => throw new ArgumentOutOfRangeException(nameof(rateType), rateType, "Unknown rate type."),
        };
    }
}
