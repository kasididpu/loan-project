using LoanProject.Application.Reports;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Reporting endpoints (phase 6) for operations/risk, all served from the Read
/// DB. "now" is resolved here and passed down so the reports run against a
/// single, testable clock reading.
/// </summary>
public static class ReportEndpoints
{
    private const int DefaultCollectionsWindowDays = 30;

    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/portfolio-summary", GetPortfolioSummaryAsync);
        app.MapGet("/reports/daily-collections", GetDailyCollectionsAsync);
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
        if (window <= 0)
            return Results.BadRequest(new { error = "windowDays must be positive." });

        var rows = await query.GetAsync(DateTime.UtcNow, window, cancellationToken);
        return Results.Ok(rows);
    }
}
