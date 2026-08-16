namespace LoanProject.Api.Endpoints;

/// <summary>Body of POST /auth/verify-otp — the MFA-pending token plus the emailed/SMS'd code.</summary>
public sealed record VerifyOtpRequest(string MfaToken, string Code);
