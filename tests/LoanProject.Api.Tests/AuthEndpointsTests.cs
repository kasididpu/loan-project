using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanProject.Api.Tests;

/// <summary>
/// End-to-end auth flows through the real HTTP pipeline: password login, the MFA
/// second factor, and the OAuth 2.0 client-credentials grant.
/// </summary>
[Collection("Api")]
public class AuthEndpointsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_NonMfaUser_IssuesAccessToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username = "admin", password = "Dev!Passw0rd" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username = "admin", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MfaUser_ThenVerifyOtp_IssuesAccessToken()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/auth/login", new { username = "officer", password = "Dev!Passw0rd" });
        login.EnsureSuccessStatusCode();
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(loginBody.GetProperty("mfaRequired").GetBoolean());
        var mfaToken = loginBody.GetProperty("mfaToken").GetString()!;

        // The capturing OTP store exposes the code the login step generated.
        var code = _factory.Otp.LastCode;
        Assert.NotNull(code);

        var verify = await client.PostAsJsonAsync("/auth/verify-otp", new { mfaToken, code });
        verify.EnsureSuccessStatusCode();
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(verifyBody.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Returns401()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/auth/login", new { username = "officer", password = "Dev!Passw0rd" });
        login.EnsureSuccessStatusCode();
        var mfaToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mfaToken").GetString()!;

        var verify = await client.PostAsJsonAsync("/auth/verify-otp", new { mfaToken, code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task ClientCredentials_ValidClient_IssuesTokenThatReadsReports()
    {
        var client = _factory.CreateClient();

        var token = await GetClientCredentialsTokenAsync(client, "loan-report-bot", "dev-oauth-client-secret-change-me");

        client.UseBearer(token);
        var reports = await client.GetAsync("/reports/portfolio-summary");
        reports.EnsureSuccessStatusCode(); // the System role is in the BackOffice policy
    }

    [Fact]
    public async Task ClientCredentials_WrongSecret_Returns401()
    {
        var client = _factory.CreateClient();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "loan-report-bot",
            ["client_secret"] = "not-the-secret",
        });
        var response = await client.PostAsync("/auth/token", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> GetClientCredentialsTokenAsync(
        HttpClient client, string clientId, string clientSecret)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });
        var response = await client.PostAsync("/auth/token", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
    }
}
