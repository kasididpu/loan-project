using LoanProject.Domain.Payments;

namespace LoanProject.Domain.Tests;

public class PaymentTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var id = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        var payment = new Payment(id, loanId, 8_884.88m, "evt_test_1", Now);

        Assert.Equal(id, payment.Id);
        Assert.Equal(loanId, payment.LoanId);
        Assert.Equal(8_884.88m, payment.Amount);
        Assert.Equal("evt_test_1", payment.StripeEventId);
        Assert.Equal(Now, payment.PaidAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Payment(Guid.NewGuid(), Guid.NewGuid(), amount, "evt_test_1", Now));
    }

    [Fact]
    public void Create_WithSubSatangAmount_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Payment(Guid.NewGuid(), Guid.NewGuid(), 100.005m, "evt_test_1", Now));
    }

    [Fact]
    public void Create_WithEmptyIds_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Payment(Guid.Empty, Guid.NewGuid(), 100m, "evt_test_1", Now));
        Assert.Throws<ArgumentException>(
            () => new Payment(Guid.NewGuid(), Guid.Empty, 100m, "evt_test_1", Now));
    }

    [Fact]
    public void Create_WithBlankStripeEventId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "  ", Now));
    }
}
