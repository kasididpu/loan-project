using System.Net;
using System.Text;

namespace LoanProject.Api.Tests;

/// <summary>
/// The Stripe webhook must treat any non-Stripe request as a 400, never a 500.
/// A missing signature header once slipped past the StripeException catch as an
/// unhandled NullReferenceException (500) — load testing found it; this pins the
/// fix (phase 10). The guard runs before the Vault fetch, so it is deterministic
/// regardless of whether Stripe test keys are seeded locally.
/// </summary>
[Collection("Api")]
public class StripeWebhookEndpointsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public StripeWebhookEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Webhook_MissingSignatureHeader_Returns400NotServerError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/webhooks/stripe",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
