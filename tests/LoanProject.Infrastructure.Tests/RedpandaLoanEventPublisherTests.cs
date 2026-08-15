using System.Text.Json;
using Confluent.Kafka;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Publisher against the real Redpanda container. Each test publishes to a
/// unique topic (auto-created in dev mode) so runs never see each other's
/// events — and never touch the app's real loan-events topic.
/// </summary>
public class RedpandaLoanEventPublisherTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static string BootstrapServers =>
        Environment.GetEnvironmentVariable("Redpanda__BootstrapServers") ?? "localhost:9092";

    private static StoredEvent Stored(long sequence, Guid aggregateId, int version) =>
        new(sequence, aggregateId, version, "PaymentReceived",
            $$"""{"PaymentId":"{{Guid.NewGuid()}}","Amount":8884.88,"InstallmentNo":{{version}}}""",
            Now);

    private static List<(string Key, LoanEventEnvelope Envelope)> ConsumeAll(string topic, int expectedCount)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        var consumed = new List<(string, LoanEventEnvelope)>();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (consumed.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result is null)
                continue;
            consumed.Add((
                result.Message.Key,
                JsonSerializer.Deserialize<LoanEventEnvelope>(result.Message.Value)!));
        }

        return consumed;
    }

    [Fact]
    public async Task PublishAsync_BatchAcrossTwoLoans_DeliversAllInPerLoanOrder()
    {
        var topic = $"loan-events-test-{Guid.NewGuid():N}";
        var loanA = Guid.NewGuid();
        var loanB = Guid.NewGuid();
        var batch = new[]
        {
            Stored(101, loanA, 1),
            Stored(102, loanB, 1),
            Stored(103, loanA, 2),
            Stored(104, loanB, 2),
            Stored(105, loanA, 3),
        };
        using var publisher = new RedpandaLoanEventPublisher(
            BootstrapServers, topic, NullLogger<RedpandaLoanEventPublisher>.Instance);

        await publisher.PublishAsync(batch, CancellationToken.None);
        var consumed = ConsumeAll(topic, batch.Length);

        Assert.Equal(batch.Length, consumed.Count);
        // Key = AggregateId, and each loan's versions arrive in order.
        foreach (var (key, envelope) in consumed)
            Assert.Equal(envelope.AggregateId.ToString(), key);
        Assert.Equal(
            new[] { 1, 2, 3 },
            consumed.Where(message => message.Envelope.AggregateId == loanA)
                .Select(message => message.Envelope.Version));
        Assert.Equal(
            new[] { 1, 2 },
            consumed.Where(message => message.Envelope.AggregateId == loanB)
                .Select(message => message.Envelope.Version));
    }

    [Fact]
    public async Task PublishAsync_Envelope_CarriesTheRawEventDataJson()
    {
        var topic = $"loan-events-test-{Guid.NewGuid():N}";
        var aggregateId = Guid.NewGuid();
        using var publisher = new RedpandaLoanEventPublisher(
            BootstrapServers, topic, NullLogger<RedpandaLoanEventPublisher>.Instance);

        await publisher.PublishAsync(new[] { Stored(201, aggregateId, 1) }, CancellationToken.None);
        var consumed = ConsumeAll(topic, 1);

        var envelope = Assert.Single(consumed).Envelope;
        Assert.Equal(201, envelope.Sequence);
        Assert.Equal("PaymentReceived", envelope.EventType);
        // EventData is a nested JSON object, not a re-encoded string —
        // consumers read the amount without a second parse.
        Assert.Equal(8884.88m, envelope.EventData.GetProperty("Amount").GetDecimal());
    }
}
