using System.Security.Claims;
using LoanProject.Application.Auth;
using LoanProject.Infrastructure.Auth;

namespace LoanProject.Api.Security;

/// <summary>
/// Turns the current HTTP request's validated claims into the ICurrentUser the
/// application layer depends on. This is the single seam where a token becomes an
/// identity, so a handler can treat Name/CustomerId/roles as verified rather than
/// caller-supplied. Bearer validation runs with MapInboundClaims off, so claims
/// are read by their raw names (sub, customer_id, role).
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public string Name => Principal?.Identity?.Name ?? "anonymous";

    public Guid? CustomerId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtTokenService.CustomerIdClaim), out var id) ? id : null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
