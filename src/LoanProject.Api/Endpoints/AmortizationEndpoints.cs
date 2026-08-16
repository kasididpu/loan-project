using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Stateless amortization preview (phase 10). Exposes the domain
/// <see cref="AmortizationCalculator"/> as a pure-compute endpoint — no auth, no
/// database, no cache — so a caller can pre-qualify a loan and a load test can
/// measure the money-calc path in isolation.
/// </summary>
public static class AmortizationEndpoints
{
    public static IEndpointRouteBuilder MapAmortization(this IEndpointRouteBuilder app)
    {
        app.MapPost("/amortization/preview", Preview);
        return app;
    }

    private static IResult Preview(AmortizationPreviewRequest request)
    {
        try
        {
            // Effective = reducing balance (annuity); Flat charges interest on the
            // original principal every month. Same calculator the aggregate uses.
            var schedule = request.RateType == RateType.Flat
                ? AmortizationCalculator.BuildFlatSchedule(request.Principal, request.AnnualRate, request.TermMonths)
                : AmortizationCalculator.BuildSchedule(request.Principal, request.AnnualRate, request.TermMonths);

            return Results.Ok(new AmortizationPreviewResponse(
                request.Principal,
                request.AnnualRate,
                request.TermMonths,
                request.RateType,
                MonthlyPayment: schedule[0].Payment,
                TotalPaid: schedule.Sum(installment => installment.Payment),
                TotalInterest: schedule.Sum(installment => installment.InterestPortion),
                Schedule: schedule));
        }
        catch (ArgumentException exception)
        {
            // ArgumentOutOfRangeException (non-positive principal/term, negative
            // rate) and ArgumentException (sub-satang principal) both land here.
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
