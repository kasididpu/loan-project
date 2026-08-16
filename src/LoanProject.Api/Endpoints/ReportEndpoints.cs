using LoanProject.Application.Auth;
using LoanProject.Application.Reports;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Reporting endpoints (phase 6) for operations/risk, all served from the Read
/// DB. "now" is resolved here and passed down so the reports run against a
/// single, testable clock reading. These expose portfolio-level data, so Phase 8
/// restricts them to the BackOffice policy (any staff role).
/// </summary>
public static class ReportEndpoints
{
    private const int DefaultCollectionsWindowDays = 30;

    // Upper bound on the requested window: untrusted input, so cap it here (the
    // trust boundary) to keep a huge value from turning the day-by-day report
    // into a CPU/memory sink. One year is well past any real reporting need.
    private const int MaxCollectionsWindowDays = 366;

    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/portfolio-summary", GetPortfolioSummaryAsync)
            .RequireAuthorization(AuthPolicies.BackOffice);
        app.MapGet("/reports/daily-collections", GetDailyCollectionsAsync)
            .RequireAuthorization(AuthPolicies.BackOffice);
        return app;
    }

    private static async Task<IResult> GetPortfolioSummaryAsync(
        IPortfolioSummaryQuery query, CancellationToken cancellationToken)
    {
        var summary = await query.GetAsync(DateTime.UtcNow, cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> GetDailyCollectionsAsync(
        int? windowDays, IDailyCollectionsQuery query, CancellationToken cancellationToken)
    {
        var window = windowDays ?? DefaultCollectionsWindowDays;
        if (window < 1 || window > MaxCollectionsWindowDays)
            return Results.BadRequest(
                new { error = $"windowDays must be between 1 and {MaxCollectionsWindowDays}." });

        var rows = await query.GetAsync(DateTime.UtcNow, window, cancellationToken);
        return Results.Ok(rows);
    }
}
