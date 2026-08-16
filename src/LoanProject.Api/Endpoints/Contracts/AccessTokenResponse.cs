using System.Text.Json.Serialization;

namespace LoanProject.Api.Endpoints;

/// <summary>
/// Token response for the login and client-credentials flows. Field names follow
/// the OAuth 2.0 convention (access_token, token_type, expires_in) rather than
/// the app's default camelCase, so standard OAuth clients read it as-is.
/// </summary>
public sealed record AccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
