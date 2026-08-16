using LoanProject.Api.Security;
using LoanProject.Application.Auth;
using LoanProject.Application.Customers;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Customer + KYC endpoints. Phase 8: onboarding is a LoanOfficer action, KYC
/// changes are locked to the Compliance policy, and a Customer may read only
/// their own record. Identity documents are stored encrypted at rest and
/// returned masked.
/// </summary>
public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomers(this IEndpointRouteBuilder app)
    {
        app.MapPost("/customers", CreateAsync).RequireAuthorization(AuthPolicies.LoanOfficer);
        app.MapPut("/customers/{id:guid}/kyc", SetKycAsync).RequireAuthorization(AuthPolicies.Compliance);
        app.MapGet("/customers/{id:guid}", GetAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        CreateCustomerHandler handler,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await handler.HandleAsync(
                request.FullName, request.NationalId, request.BankAccountNumber, cancellationToken);

            // Log-masking demo: the Customer is destructured with {@...}, but the
            // Serilog policy masks NationalId/BankAccountNumber, so no PII reaches Seq.
            loggerFactory.CreateLogger("Customers").LogInformation("Customer onboarded {@Customer}", customer);

            return Results.Created($"/customers/{customer.Id}", new { id = customer.Id });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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
        Guid id, ICustomerRepository customers, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        // IDOR guard: a Customer may read only their own record (404, not 403).
        if (currentUser.IsInRole(Roles.Customer) && id != currentUser.CustomerId)
            return Results.NotFound();

        var customer = await customers.FindAsync(id, cancellationToken);
        return customer is null
            ? Results.NotFound()
            : Results.Ok(new
            {
                customer.Id,
                customer.FullName,
                customer.KycStatus,
                NationalId = SensitiveDataMasker.MaskTail(customer.NationalId),
                BankAccountNumber = SensitiveDataMasker.MaskTail(customer.BankAccountNumber),
                customer.CreatedAtUtc,
            });
    }
}
