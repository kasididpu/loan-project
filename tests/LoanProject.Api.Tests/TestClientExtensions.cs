using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanProject.Api.Tests;

/// <summary>Small helpers for driving the auth flow from tests.</summary>
internal static class TestClientExtensions
{
    /// <summary>Logs in a non-MFA seeded user and returns the bearer access token.</summary>
    public static async Task<string> GetAccessTokenAsync(
        this HttpClient client, string username, string password = "Dev!Passw0rd")
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    public static void UseBearer(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
