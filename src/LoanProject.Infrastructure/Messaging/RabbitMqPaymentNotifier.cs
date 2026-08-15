using System.Text.Json;
using LoanProject.Application.Payments;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LoanProject.Infrastructure.Messaging;

/// <summary>
/// Publishes payment notices to a durable RabbitMQ queue. Best-effort by the
/// port's contract: any broker failure is logged and swallowed — the webhook
/// that calls this has already recorded the money and must return 200.
/// </summary>
public sealed class RabbitMqPaymentNotifier : IPaymentNotifier, IDisposable
{
    public const string DefaultQueueName = "payment-notifications";

    private readonly ConnectionFactory _connectionFactory;
    private readonly string _queueName;
    private readonly ILogger<RabbitMqPaymentNotifier> _logger;
    private readonly object _gate = new();

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPaymentNotifier(
        string amqpUri, string queueName, ILogger<RabbitMqPaymentNotifier> logger)
    {
        if (string.IsNullOrWhiteSpace(amqpUri))
            throw new ArgumentException("AMQP uri is required.", nameof(amqpUri));
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name is required.", nameof(queueName));

        // Automatic recovery re-establishes the connection after a broker
        // restart; publishes in the gap still fail and are logged below.
        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(amqpUri),
            AutomaticRecoveryEnabled = true,
        };
        _queueName = queueName;
        _logger = logger;
    }

    public Task NotifyPaymentReceivedAsync(PaymentReceivedNotice notice, CancellationToken cancellationToken)
    {
        try
        {
            // IModel is not thread-safe; concurrent webhooks serialize here.
            // Publishing is a local socket write — the lock is held briefly.
            lock (_gate)
            {
                var channel = EnsureChannel();
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; // survive a broker restart, like the queue itself
                properties.ContentType = "application/json";

                channel.BasicPublish(
                    exchange: string.Empty, // default exchange routes by queue name
                    routingKey: _queueName,
                    basicProperties: properties,
                    body: JsonSerializer.SerializeToUtf8Bytes(notice));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Payment notification for stripe event {StripeEventId} could not be queued.",
                notice.StripeEventId);
        }

        return Task.CompletedTask;
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _channel?.Dispose();
        if (_connection is not { IsOpen: true })
        {
            _connection?.Dispose();
            _connection = _connectionFactory.CreateConnection();
        }

        _channel = _connection.CreateModel();
        // Both sides declare the same durable queue so start order never
        // matters; declaring an existing queue with equal settings is a no-op.
        _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        return _channel;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
