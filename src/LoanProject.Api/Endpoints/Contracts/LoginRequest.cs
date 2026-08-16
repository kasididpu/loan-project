namespace LoanProject.Api.Endpoints;

/// <summary>Body of POST /auth/login.</summary>
public sealed record LoginRequest(string Username, string Password);
