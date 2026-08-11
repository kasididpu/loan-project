using LoanProject.Domain.Customers;

namespace LoanProject.Domain.Tests;

public class CustomerTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();

        var customer = new Customer(id, "Somchai Jaidee", Now);

        Assert.Equal(id, customer.Id);
        Assert.Equal("Somchai Jaidee", customer.FullName);
        Assert.Equal(Now, customer.CreatedAtUtc);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Customer(Guid.Empty, "Somchai Jaidee", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankFullName_Throws(string fullName)
    {
        Assert.Throws<ArgumentException>(() => new Customer(Guid.NewGuid(), fullName, Now));
    }
}
