using System.Net;
using System.Net.Http.Json;
using LoanProject.Infrastructure.Persistence;

namespace LoanProject.Api.Tests;

/// <summary>
/// Role-based access control through the real authorization pipeline: the right
/// role gets in, the wrong role is forbidden, and no token is unauthorized.
/// </summary>
[Collection("Api")]
public class AuthorizationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Reports_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/reports/portfolio-summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reports_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somsri"));

        var response = await client.GetAsync("/reports/portfolio-summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reports_AsAdmin_Returns200()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("admin"));

        var response = await client.GetAsync("/reports/portfolio-summary");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ApproveLoan_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somsri"));

        // The policy denies before the handler runs, so the loan's state is irrelevant.
        var response = await client.PostAsync($"/loans/{DevDataSeeder.SeedLoanId}/approve", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetKyc_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somsri"));

        var response = await client.PutAsJsonAsync(
            $"/customers/{DevDataSeeder.SeedCustomerNewId}/kyc", new { status = "Verified" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
