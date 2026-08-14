using LoanProject.Application.Payments;
using LoanProject.Application.Secrets;
using Stripe;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Stripe webhook (phase 4). Authenticity comes from the Stripe-Signature
/// header: HMAC over the raw body with the endpoint's signing secret, which
/// lives in Vault — a request that fails verification never reaches any
/// business code.
/// </summary>
public static class StripeWebhookEndpoints
{
    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/stripe", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        ISecretProvider secretProvider,
        RecordStripePaymentHandler handler,
        CancellationToken cancellationToken)
    {
        var payload = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
        var webhookSecret = await secretProvider.GetSecretAsync("StripeWebhookSecret", cancellationToken);

        Event stripeEvent;
        try
        {
            // throwOnApiVersionMismatch: false — the account's API version
            // moves ahead of the SDK's pinned one; the HMAC signature check
            // (the actual security boundary) is unaffected by this flag.
            stripeEvent = EventUtility.ConstructEvent(
                payload, request.Headers["Stripe-Signature"], webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            // Wrong or missing signature: not from Stripe. No detail leaks back.
            return Results.BadRequest();
        }

        if (stripeEvent.Type == "payment_intent.succeeded")
        {
            var intent = (PaymentIntent)stripeEvent.Data.Object;
            await handler.HandleAsync(
                new StripePaymentNotification(
                    Guid.Parse(intent.Metadata["loanId"]),
                    int.Parse(intent.Metadata["installmentNo"]),
                    // Stripe amounts are in the smallest currency unit (satang
                    // for THB): 888488 -> 8,884.88. Division by 100m is exact
                    // in decimal — no rounding is involved or allowed here.
                    intent.AmountReceived / 100m,
                    stripeEvent.Id,
                    stripeEvent.Created),
                cancellationToken);
        }

        // Unhandled event types are acknowledged so Stripe stops retrying them.
        return Results.Ok();
    }
}
