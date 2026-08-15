using System.Text.Json;
using LoanProject.Application.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LoanProject.Infrastructure.Messaging;

/// <summary>
/// Consumes payment notices and "sends" the customer notification — here a
/// structured log line stands in for the SMS/email provider. Manual ack
/// after processing: a consumer crash mid-message means redelivery, and the
/// deduplicator absorbs the resulting repeat.
/// </summary>
public sealed class PaymentNotificationConsumer(
    string amqpUri,
    string queueName,
    PaymentNotificationDeduplicator deduplicator,
    ILogger<PaymentNotificationConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Async consumer dispatch + automatic recovery: after the
                // initial connect succeeds, the client re-attaches consumers
                // through broker restarts on its own.
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(amqpUri),
                    AutomaticRecoveryEnabled = true,
                    DispatchConsumersAsync = true,
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
                // Hand this consumer a few messages at a time — unacked work
                // stays small if it dies.
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += (_, delivery) =>
                {
                    Handle(channel, delivery);
                    return Task.CompletedTask;
                };
                channel.BasicConsume(queueName, autoAck: false, consumer);

                logger.LogInformation("Payment notification consumer started on '{Queue}'.", queueName);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Payment notification consumer cannot reach the broker; retrying in {Delay}.",
                    ConnectRetryDelay);
                try
                {
                    await Task.Delay(ConnectRetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Payment notification consumer stopped.");
    }

    private void Handle(IModel channel, BasicDeliverEventArgs delivery)
    {
        try
        {
            var notice = JsonSerializer.Deserialize<PaymentReceivedNotice>(delivery.Body.Span)
                ?? throw new JsonException("Notice deserialized to null.");

            if (deduplicator.TryRegister(notice.StripeEventId))
            {
                // The simulated send: in production this line is the SMS/email
                // provider call. Amount is safe to log — dev seed data only.
                logger.LogInformation(
                    "NOTIFY customer: payment of {Amount} received for loan {LoanId} installment {InstallmentNo}.",
                    notice.Amount, notice.LoanId, notice.InstallmentNo);
            }
            else
            {
                logger.LogInformation(
                    "Duplicate payment notice for stripe event {StripeEventId} skipped.",
                    notice.StripeEventId);
            }

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (JsonException exception)
        {
            // A malformed message would loop forever on requeue — drop it
            // and keep the evidence in the log.
            logger.LogError(exception, "Unreadable payment notice dropped.");
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
