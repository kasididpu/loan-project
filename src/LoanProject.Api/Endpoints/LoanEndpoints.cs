using LoanProject.Application.Customers;
using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Loan command + status endpoints (phase 6). Commands go through the event
/// store (write side); GET reads the eventually-consistent Read DB. The split is
/// the whole point of CQRS: a write and its read are not guaranteed visible in
/// the same instant.
/// No authentication yet — by roadmap design the whole app is unauthenticated
/// until Phase 8, which adds auth, derives the officer identity for approve/reject
/// from claims (dropping ApprovedBy/RejectedBy from the request bodies), and
/// scopes GET /loans/{id} to the caller instead of returning any loan by id.
/// </summary>
public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoans(this IEndpointRouteBuilder app)
    {
        app.MapPost("/loans", OriginateAsync);
        app.MapPost("/loans/{id:guid}/approve", ApproveAsync);
        app.MapPost("/loans/{id:guid}/disburse", DisburseAsync);
        app.MapPost("/loans/{id:guid}/reject", RejectAsync);
        app.MapGet("/loans/{id:guid}", GetStatusAsync);
        return app;
    }

    private static async Task<IResult> OriginateAsync(
        OriginateLoanRequest request, OriginateLoanHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            var loanId = await handler.HandleAsync(
                request.CustomerId, request.Principal, request.RateType, request.TermMonths, cancellationToken);
            return Results.Created($"/loans/{loanId}", new { loanId });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static Task<IResult> ApproveAsync(
        Guid id, ApproveLoanRequest request, ApproveLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, request.ApprovedBy, cancellationToken));

    private static Task<IResult> DisburseAsync(
        Guid id, DisburseLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, cancellationToken));

    private static Task<IResult> RejectAsync(
        Guid id, RejectLoanRequest request, RejectLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, request.RejectedBy, request.Reason, cancellationToken));

    private static async Task<IResult> GetStatusAsync(
        Guid id, ILoanStatusQuery query, CancellationToken cancellationToken)
    {
        var view = await query.GetAsync(id, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    // All command transitions share the same failure mapping: unknown loan 404,
    // concurrent/illegal state change 409, a failed KYC gate 422, bad argument 400.
    private static async Task<IResult> RunTransitionAsync(Func<Task> transition)
    {
        try
        {
            await transition();
            return Results.NoContent();
        }
        catch (LoanNotFoundException)
        {
            return Results.NotFound();
        }
        catch (LoanConcurrencyException)
        {
            return Results.Conflict(new { error = "The loan changed concurrently; reload and retry." });
        }
        catch (InvalidLoanTransitionException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (KycNotVerifiedException exception)
        {
            // Valid request, but a business precondition is unmet → 422, not 409.
            return Results.UnprocessableEntity(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
