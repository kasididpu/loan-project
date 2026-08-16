using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanProject.Infrastructure.Persistence;

namespace LoanProject.Api.Tests;

/// <summary>
/// GET /loans/{id}/events — the read-only audit trail (phase 11.5). Staff-only,
/// and the events come back in ledger order with their payloads inlined as JSON.
/// </summary>
[Collection("Api")]
public class LoanEventsEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public LoanEventsEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_SeedLoan_AsStaff_ReturnsAuditTrailInOrder()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("admin"));

        var response = await client.GetAsync($"/loans/{DevDataSeeder.SeedLoanId}/events");

        response.EnsureSuccessStatusCode();
        var events = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Seed loan: originated -> approved -> disbursed -> first payment = 4 events.
        Assert.True(events.GetArrayLength() >= 4);
        Assert.Equal("LoanOriginated", events[0].GetProperty("eventType").GetString());
        Assert.Equal(1, events[0].GetProperty("version").GetInt32());
        // The payload is inlined JSON, not an escaped string.
        Assert.Equal(JsonValueKind.Object, events[0].GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task GetEvents_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/loans/{DevDataSeeder.SeedLoanId}/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
