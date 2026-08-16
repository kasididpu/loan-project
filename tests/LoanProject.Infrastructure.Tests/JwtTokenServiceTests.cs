using System.IdentityModel.Tokens.Jwt;
using LoanProject.Application.Auth;
using LoanProject.Infrastructure.Auth;

namespace LoanProject.Infrastructure.Tests;

/// <summary>Pure unit tests for JWT issuance/validation — no infrastructure.</summary>
public class JwtTokenServiceTests
{
    private static JwtTokenService NewService() => new(new JwtOptions(
        Issuer: "loan-api",
        Audience: "loan-api-clients",
        SigningKey: "unit-test-signing-key-that-is-long-enough-0123456789",
        AccessTokenLifetime: TimeSpan.FromMinutes(60),
        MfaTokenLifetime: TimeSpan.FromMinutes(5)));

    [Fact]
    public void IssueAccessToken_EmbedsSubjectNameRoleAndCustomerId()
    {
        var service = NewService();
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var token = service.IssueAccessToken(userId, "somsri", new[] { Roles.Customer }, customerId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(userId.ToString(), jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("somsri", jwt.Claims.Single(c => c.Type == "unique_name").Value);
        Assert.Equal(Roles.Customer, jwt.Claims.Single(c => c.Type == JwtTokenService.RoleClaim).Value);
        Assert.Equal(customerId.ToString(), jwt.Claims.Single(c => c.Type == JwtTokenService.CustomerIdClaim).Value);
    }

    [Fact]
    public void IssueAccessToken_WithoutCustomer_OmitsCustomerIdClaim()
    {
        var token = NewService().IssueAccessToken(Guid.NewGuid(), "officer", new[] { Roles.LoanOfficer }, null);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenService.CustomerIdClaim);
    }

    [Fact]
    public void ReadMfaPendingSubject_RoundTripsForMfaToken()
    {
        var service = NewService();
        var userId = Guid.NewGuid();

        var mfaToken = service.IssueMfaPendingToken(userId, "officer");

        Assert.Equal(userId, service.ReadMfaPendingSubject(mfaToken));
    }

    [Fact]
    public void ReadMfaPendingSubject_RejectsAccessTokenAsMfaToken()
    {
        var service = NewService();
        var accessToken = service.IssueAccessToken(Guid.NewGuid(), "officer", new[] { Roles.LoanOfficer }, null);

        // An access token must never be usable at the OTP-verification step.
        Assert.Null(service.ReadMfaPendingSubject(accessToken));
    }

    [Fact]
    public void ReadMfaPendingSubject_RejectsGarbage()
    {
        Assert.Null(NewService().ReadMfaPendingSubject("not-a-token"));
    }
}
