using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LoanProject.Infrastructure.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// CQRS projector: consumes loan-events from Redpanda and drives the Read
/// database through <see cref="LoanReadModelProjection"/>. One consumer group,
/// starting at the earliest offset so a fresh Read DB rebuilds from the entire
/// history. Offsets are committed by hand, only after the projection has
/// committed — so a crash replays the last event, which is safe because the
/// projection is idempotent (at-least-once delivery + dedupe = effectively-once).
/// </summary>
public sealed class LoanReadModelProjector : BackgroundService
{
    public const string ConsumerGroup = "loan-read-model-projector";

    private readonly string _bootstrapServers;
    private readonly string _topic;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoanReadModelProjector> _logger;

    public LoanReadModelProjector(
        string bootstrapServers,
        string topic,
        IServiceScopeFactory scopeFactory,
        ILogger<LoanReadModelProjector> logger)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
            throw new ArgumentException("Bootstrap servers are required.", nameof(bootstrapServers));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));

        _bootstrapServers = bootstrapServers;
        _topic = topic;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let host startup finish before the blocking consume loop begins.
        await Task.Yield();

        // On a fresh broker the topic does not exist until someone first produces
        // to it. Create it up front (idempotent) so the consumer never subscribes
        // to a missing topic — deterministic for a clone-and-run setup.
        await EnsureTopicExistsAsync(stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = ConsumerGroup,
            // A group with no committed offset starts at the beginning: the
            // Read DB is (re)built from the whole event history.
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Commit only after the projection is durable, by hand.
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => _logger.LogWarning(
                "Read-model consumer error: {Reason} (fatal: {IsFatal})", error.Reason, error.IsFatal))
            .Build();

        consumer.Subscribe(_topic);
        _logger.LogInformation(
            "Read-model projector subscribed to {Topic} as group {Group}.", _topic, ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    // A transient consume failure — e.g. the topic is not visible
                    // yet on a fresh broker — must not fault the host (the default
                    // BackgroundService behaviour would stop it). Back off and
                    // retry; the dispatcher's first publish makes the topic appear.
                    _logger.LogWarning(
                        exception, "Read-model consume failed ({Reason}); retrying.", exception.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                if (result?.Message is null)
                    continue;

                try
                {
                    var envelope = JsonSerializer.Deserialize<LoanEventEnvelope>(result.Message.Value)
                        ?? throw new InvalidOperationException("loan-events message deserialized to null.");

                    // ReadDbContext and the projection are scoped: a fresh scope
                    // per message, same lifetime story as a web request.
                    using var scope = _scopeFactory.CreateScope();
                    var projection = scope.ServiceProvider.GetRequiredService<LoanReadModelProjection>();
                    await projection.ProjectAsync(envelope, stoppingToken);

                    // Durable now — advance the offset so this event is not
                    // replayed on the next poll.
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // Do not commit: the message is redelivered. The projection
                    // is idempotent, so a transient blip heals on retry; back
                    // off so a persistent fault does not spin the CPU.
                    _logger.LogError(exception,
                        "Projecting loan event at offset {Offset} failed; will retry.", result.Offset);
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <summary>
    /// Creates the loan-events topic if it is missing. Idempotent and best-effort:
    /// "already exists" is the normal case, and any other failure falls through to
    /// the resilient consume loop, which retries once the topic appears.
    /// </summary>
    private async Task EnsureTopicExistsAsync(CancellationToken cancellationToken)
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = _bootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync(new[]
            {
                new TopicSpecification { Name = _topic, NumPartitions = 1, ReplicationFactor = 1 },
            });
            _logger.LogInformation("Created topic {Topic}.", _topic);
        }
        catch (CreateTopicsException exception)
            when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Already there — a previous run, or the dispatcher's first publish.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not pre-create topic {Topic}; relying on consume retry.", _topic);
        }
    }
}
