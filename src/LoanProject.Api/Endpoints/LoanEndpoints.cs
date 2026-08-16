using System.Text.Json;
using LoanProject.Application.Auth;
using LoanProject.Application.Customers;
using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Loan command + status endpoints. Commands go through the event store (write
/// side); GET reads the eventually-consistent Read DB. The split is the whole
/// point of CQRS: a write and its read are not guaranteed visible in the same
/// instant.
/// Phase 8: lifecycle commands require the LoanOfficer policy and take the acting
/// officer from the token (no ApprovedBy/RejectedBy in the body); GET is scoped
/// so a Customer can read only their own loan.
/// </summary>
public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoans(this IEndpointRouteBuilder app)
    {
        app.MapPost("/loans", OriginateAsync).RequireAuthorization(AuthPolicies.LoanOfficer);
        app.MapPost("/loans/{id:guid}/approve", ApproveAsync).RequireAuthorization(AuthPolicies.LoanOfficer);
        app.MapPost("/loans/{id:guid}/disburse", DisburseAsync).RequireAuthorization(AuthPolicies.LoanOfficer);
        app.MapPost("/loans/{id:guid}/reject", RejectAsync).RequireAuthorization(AuthPolicies.LoanOfficer);
        app.MapGet("/loans/{id:guid}", GetStatusAsync).RequireAuthorization();
        // Audit trail (phase 11.5): the loan's full event stream, staff-only.
        app.MapGet("/loans/{id:guid}/events", GetEventsAsync).RequireAuthorization(AuthPolicies.BackOffice);
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
        Guid id, ApproveLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, cancellationToken));

    private static Task<IResult> DisburseAsync(
        Guid id, DisburseLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, cancellationToken));

    private static Task<IResult> RejectAsync(
        Guid id, RejectLoanRequest request, RejectLoanHandler handler, CancellationToken cancellationToken) =>
        RunTransitionAsync(() => handler.HandleAsync(id, request.Reason, cancellationToken));

    private static async Task<IResult> GetStatusAsync(
        Guid id, ILoanStatusQuery query, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var view = await query.GetAsync(id, cancellationToken);
        if (view is null)
            return Results.NotFound();

        // IDOR guard: a Customer may read only their own loan; staff and system
        // callers may read any. 404 (not 403) so a probing customer cannot even
        // learn that another customer's loan exists.
        if (currentUser.IsInRole(Roles.Customer) && view.CustomerId != currentUser.CustomerId)
            return Results.NotFound();

        return Results.Ok(view);
    }

    private static async Task<IResult> GetEventsAsync(
        Guid id, ILoanEventStreamQuery query, CancellationToken cancellationToken)
    {
        var events = await query.GetAsync(id, cancellationToken);
        // No events for this id means no such loan — 404, same as GET status.
        if (events.Count == 0)
            return Results.NotFound();

        // Inline each stored payload as JSON so the audit trail reads cleanly
        // instead of showing an escaped string.
        var view = events
            .Select(entry => new LoanEventView(
                entry.Version,
                entry.EventType,
                entry.OccurredAtUtc,
                JsonSerializer.Deserialize<JsonElement>(entry.EventDataJson)))
            .ToList();

        return Results.Ok(view);
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
