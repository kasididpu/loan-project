using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Rate lookup (phase 5) — exists mainly to make the Redis cache observable:
/// first call per (rateType, term) pays the slow source, repeats within the
/// TTL come back from cache. Compare timings in Swagger or k6.
/// </summary>
public static class RateEndpoints
{
    public static IEndpointRouteBuilder MapRates(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rates/{rateType}/{termMonths:int}", GetRateAsync);
        return app;
    }

    private static async Task<IResult> GetRateAsync(
        RateType rateType,
        int termMonths,
        IInterestRateLookup rateLookup,
        CancellationToken cancellationToken)
    {
        if (termMonths <= 0)
            return Results.BadRequest(new { error = "termMonths must be positive." });

        var annualRate = await rateLookup.GetAnnualRateAsync(rateType, termMonths, cancellationToken);
        return Results.Ok(new { rateType = rateType.ToString(), termMonths, annualRate });
    }
}
