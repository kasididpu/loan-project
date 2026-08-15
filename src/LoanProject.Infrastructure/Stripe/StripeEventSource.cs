using LoanProject.Application.Reconciliation;
using LoanProject.Application.Secrets;
using Stripe;

namespace LoanProject.Infrastructure.Stripe;

/// <summary>
/// Reads Stripe's own record of successful payments for reconciliation —
/// the first consumer of StripeSecretKey from Vault. Calls made through the
/// SDK pin the SDK's API version, so responses always match what it can
/// deserialize (unlike webhooks, whose payload follows the account version).
/// </summary>
public sealed class StripeEventSource(ISecretProvider secretProvider) : IStripeEventSource
{
    public async Task<IReadOnlyList<StripePaymentEvent>> ListRecentPaymentEventsAsync(
        DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var apiKey = await secretProvider.GetSecretAsync("StripeSecretKey", cancellationToken);
        var eventService = new EventService(new StripeClient(apiKey));

        var stripeEvents = await eventService.ListAsync(
            new EventListOptions
            {
                Type = "payment_intent.succeeded",
                Created = new DateRangeOptions { GreaterThanOrEqual = sinceUtc },
                Limit = 100,
            },
            cancellationToken: cancellationToken);

        var results = new List<StripePaymentEvent>();
        foreach (var stripeEvent in stripeEvents)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;

            // Same satang -> baht conversion as the webhook: exact division,
            // no rounding involved or allowed.
            var amount = (intent?.AmountReceived ?? 0L) / 100m;

            Guid? loanId = null;
            int? installmentNo = null;
            if (intent?.Metadata is not null
                && intent.Metadata.TryGetValue("loanId", out var loanIdRaw)
                && Guid.TryParse(loanIdRaw, out var parsedLoanId))
            {
                loanId = parsedLoanId;
                if (intent.Metadata.TryGetValue("installmentNo", out var installmentRaw)
                    && int.TryParse(installmentRaw, out var parsedInstallment))
                    installmentNo = parsedInstallment;
            }

            results.Add(new StripePaymentEvent(
                stripeEvent.Id, loanId, installmentNo, amount, stripeEvent.Created));
        }

        return results;
    }
}
