using LoanProject.Infrastructure.Messaging;

namespace LoanProject.Infrastructure.Tests;

public class PaymentNotificationDeduplicatorTests
{
    [Fact]
    public void TryRegister_FirstTime_ReturnsTrue()
    {
        var deduplicator = new PaymentNotificationDeduplicator();

        Assert.True(deduplicator.TryRegister("evt_dd_1"));
    }

    [Fact]
    public void TryRegister_SameIdAgain_ReturnsFalse()
    {
        var deduplicator = new PaymentNotificationDeduplicator();
        deduplicator.TryRegister("evt_dd_1");

        // At-least-once delivery makes repeats normal; notifying twice is not.
        Assert.False(deduplicator.TryRegister("evt_dd_1"));
    }

    [Fact]
    public void TryRegister_DifferentIds_AreIndependent()
    {
        var deduplicator = new PaymentNotificationDeduplicator();
        deduplicator.TryRegister("evt_dd_1");

        Assert.True(deduplicator.TryRegister("evt_dd_2"));
    }
}
