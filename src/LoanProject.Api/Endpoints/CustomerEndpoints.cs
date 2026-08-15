using LoanProject.Application.Customers;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Customer + KYC endpoints (phase 7). Setting KYC is a simulated compliance
/// action — no external verification system. No authentication yet: Phase 8 will
/// restrict KYC changes to a compliance/back-office role; today the app is
/// unauthenticated by roadmap design.
/// </summary>
public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomers(this IEndpointRouteBuilder app)
    {
        app.MapPut("/customers/{id:guid}/kyc", SetKycAsync);
        app.MapGet("/customers/{id:guid}", GetAsync);
        return app;
    }

    private static async Task<IResult> SetKycAsync(
        Guid id, SetKycStatusRequest request, SetKycStatusHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(id, request.Status, cancellationToken);
            return Results.NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetAsync(
        Guid id, ICustomerRepository customers, CancellationToken cancellationToken)
    {
        var customer = await customers.FindAsync(id, cancellationToken);
        return customer is null
            ? Results.NotFound()
            : Results.Ok(new { customer.Id, customer.FullName, customer.KycStatus, customer.CreatedAtUtc });
    }
}
