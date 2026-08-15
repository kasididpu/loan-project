using System.Text.Json;
using Confluent.Kafka;
using LoanProject.Infrastructure.EventStore;
using Microsoft.Extensions.Logging;

namespace LoanProject.Infrastructure.Streaming;

/// <summary>
/// Kafka-protocol producer for the loan-events topic (Redpanda speaks Kafka).
/// Key = AggregateId, so all events of one loan land on one partition and
/// keep their order no matter how the topic scales.
/// </summary>
public sealed class RedpandaLoanEventPublisher : ILoanEventPublisher, IDisposable
{
    public const string DefaultTopic = "loan-events";

    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public RedpandaLoanEventPublisher(
        string bootstrapServers, string topic, ILogger<RedpandaLoanEventPublisher> logger)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
            throw new ArgumentException("Bootstrap servers are required.", nameof(bootstrapServers));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));
        _topic = topic;

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            // Idempotent producer: broker-side retries cannot duplicate or
            // reorder within a partition — the cursor already gives
            // at-least-once across restarts; this keeps single-run retries
            // from adding noise on top.
            EnableIdempotence = true,
            Acks = Acks.All,
            // Fail a send after 10s instead of the 5-minute default, so a
            // dead broker surfaces as an exception the dispatcher can back
            // off on, not a silent stall.
            MessageTimeoutMs = 10_000,
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogWarning(
                "Redpanda producer error: {Reason} (fatal: {IsFatal})", error.Reason, error.IsFatal))
            .SetLogHandler((_, message) => logger.LogDebug(
                "Redpanda producer: {Message}", message.Message))
            .Build();
    }

    public async Task PublishAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)
    {
        foreach (var storedEvent in events)
        {
            var envelope = new LoanEventEnvelope(
                storedEvent.Sequence,
                storedEvent.AggregateId,
                storedEvent.Version,
                storedEvent.EventType,
                JsonDocument.Parse(storedEvent.EventData).RootElement.Clone(),
                storedEvent.OccurredAtUtc);

            // Awaiting each send keeps cross-aggregate order and means an
            // unacknowledged event stops the batch before the cursor moves
            // past it. Throughput is not the constraint here; the delivery
            // guarantee is.
            await _producer.ProduceAsync(
                _topic,
                new Message<string, string>
                {
                    Key = storedEvent.AggregateId.ToString(),
                    Value = JsonSerializer.Serialize(envelope),
                },
                cancellationToken);
        }
    }

    public void Dispose() => _producer.Dispose();
}
