using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanProject.Infrastructure.Persistence;
using LoanProject.Infrastructure.ReadModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanProject.Api.Tests;

/// <summary>
/// Object-level authorization (IDOR): a Customer may read only their own records,
/// staff may read any, and PII comes back masked.
/// </summary>
[Collection("Api")]
public class IdorTests
{
    private readonly CustomWebApplicationFactory _factory;

    public IdorTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCustomer_OwningCustomer_Returns200()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somsri"));

        var response = await client.GetAsync($"/customers/{DevDataSeeder.SeedCustomerWithLoanId}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCustomer_OtherCustomer_Returns404()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somchai"));

        var response = await client.GetAsync($"/customers/{DevDataSeeder.SeedCustomerWithLoanId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Onboard_ThenGetCustomer_ReturnsMaskedNationalId()
    {
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("admin"));

        // Onboard a customer with a known national id, then read it back: it is
        // stored encrypted and returned masked — never in the clear. Self-contained
        // so it does not depend on what the seed customers already have on disk.
        const string nationalId = "1234512345123";
        var create = await client.PostAsJsonAsync(
            "/customers", new { fullName = "Masking Test", nationalId, bankAccountNumber = "123-4-56789-0" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var response = await client.GetAsync($"/customers/{id}");

        response.EnsureSuccessStatusCode();
        var masked = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nationalId").GetString();
        Assert.NotNull(masked);
        Assert.DoesNotContain(nationalId, masked);  // full value never exposed
        Assert.Contains('•', masked!);              // masked
    }

    [Fact]
    public async Task GetLoan_OwningCustomer_Returns200()
    {
        EnsureLoanReadModelSeeded();
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somsri"));

        var response = await client.GetAsync($"/loans/{DevDataSeeder.SeedLoanId}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetLoan_NonOwningCustomer_Returns404()
    {
        EnsureLoanReadModelSeeded();
        var client = _factory.CreateClient();
        client.UseBearer(await client.GetAccessTokenAsync("somchai"));

        var response = await client.GetAsync($"/loans/{DevDataSeeder.SeedLoanId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The projector is disabled in-test, so seed the read-model row directly.
    private void EnsureLoanReadModelSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
        if (db.Loans.Any(l => l.LoanId == DevDataSeeder.SeedLoanId))
            return;

        var seedDate = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        db.Loans.Add(new LoanReadModel
        {
            LoanId = DevDataSeeder.SeedLoanId,
            CustomerId = DevDataSeeder.SeedCustomerWithLoanId,
            Status = "Active",
            Principal = 100_000m,
            AnnualRate = 0.12m,
            RateType = "Effective",
            TermMonths = 12,
            OutstandingBalance = 92_000m,
            NextInstallmentNo = 2,
            TotalPaid = 8_884.88m,
            InstallmentsPaid = 1,
            OriginatedAtUtc = seedDate,
            LastProjectedVersion = 4,
            UpdatedAtUtc = seedDate,
        });
        db.SaveChanges();
    }
}
