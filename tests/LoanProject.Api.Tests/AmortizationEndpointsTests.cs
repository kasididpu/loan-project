using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanProject.Api.Tests;

/// <summary>
/// The stateless amortization preview (phase 10 load-test target): anonymous,
/// pure compute. Verifies endpoint wiring and that the returned schedule closes
/// at 0.00 — the domain calculator's own edge cases are unit-tested elsewhere.
/// </summary>
[Collection("Api")]
public class AmortizationEndpointsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AmortizationEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Effective")]
    [InlineData("Flat")]
    public async Task Preview_ValidLoan_ReturnsScheduleClosingAtZero(string rateType)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/amortization/preview",
            new { principal = 100_000m, annualRate = 0.12m, termMonths = 12, rateType });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var schedule = body.GetProperty("schedule");
        Assert.Equal(12, schedule.GetArrayLength());
        Assert.True(body.GetProperty("monthlyPayment").GetDecimal() > 0);
        Assert.True(body.GetProperty("totalInterest").GetDecimal() > 0);

        // The final installment always pays the exact remainder — the loan closes.
        var last = schedule[schedule.GetArrayLength() - 1];
        Assert.Equal(0m, last.GetProperty("remainingBalance").GetDecimal());
    }

    [Fact]
    public async Task Preview_NegativePrincipal_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/amortization/preview",
            new { principal = -1m, annualRate = 0.12m, termMonths = 12, rateType = "Effective" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
