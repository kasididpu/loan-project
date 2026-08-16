using LoanProject.Application.Auth;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Test double for the authenticated caller. Handler tests only need the Name
/// (recorded as approvedBy/disbursedBy) and optionally a CustomerId; endpoint
/// authorization is out of scope for handler-level tests.
/// </summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string name = "test-officer") => Name = name;

    public bool IsAuthenticated => true;
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string Name { get; }
    public Guid? CustomerId { get; init; }
    public bool IsInRole(string role) => false;
}
