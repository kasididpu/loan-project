using System.Text.Json;
using LoanProject.Application.Payments;
using LoanProject.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Notifier against the real RabbitMQ container. Each test uses (and then
/// deletes) its own queue, so the app's real payment-notifications queue is
/// never touched.
/// </summary>
public class RabbitMqPaymentNotifierTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static string AmqpUri =>
        Environment.GetEnvironmentVariable("ConnectionStrings__RabbitMq")
        ?? "amqp://guest:guest@localhost:5672";

    [Fact]
    public async Task NotifyPaymentReceivedAsync_BrokerUp_NoticeLandsOnTheQueue()
    {
        var queueName = $"payment-notifications-test-{Guid.NewGuid():N}";
        var notice = new PaymentReceivedNotice(
            Guid.NewGuid(), 3, 8884.88m, $"evt_mq_{Guid.NewGuid():N}", Now);

        var factory = new ConnectionFactory { Uri = new Uri(AmqpUri) };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        try
        {
            using var notifier = new RabbitMqPaymentNotifier(
                AmqpUri, queueName, NullLogger<RabbitMqPaymentNotifier>.Instance);
            await notifier.NotifyPaymentReceivedAsync(notice, CancellationToken.None);

            // BasicPublish is buffered client-side — poll briefly instead of
            // assuming the broker has the message the instant the call returns.
            RabbitMQ.Client.BasicGetResult? delivery = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (delivery is null && DateTime.UtcNow < deadline)
            {
                delivery = channel.BasicGet(queueName, autoAck: true);
                if (delivery is null)
                    await Task.Delay(100);
            }
            Assert.NotNull(delivery);
            var received = JsonSerializer.Deserialize<PaymentReceivedNotice>(delivery!.Body.Span);
            Assert.Equal(notice, received);
        }
        finally
        {
            channel.QueueDelete(queueName);
        }
    }

    [Fact]
    public async Task NotifyPaymentReceivedAsync_BrokerUnreachable_SwallowsTheFailure()
    {
        // Port 1 answers nothing: the publish fails fast inside the client.
        using var notifier = new RabbitMqPaymentNotifier(
            "amqp://guest:guest@localhost:1", "unreachable-test",
            NullLogger<RabbitMqPaymentNotifier>.Instance);

        // Best-effort contract: the caller (the webhook path) must not see
        // the broker outage as an exception.
        await notifier.NotifyPaymentReceivedAsync(
            new PaymentReceivedNotice(Guid.NewGuid(), 1, 100m, "evt_mq_down", Now),
            CancellationToken.None);
    }
}
