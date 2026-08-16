using LoanProject.Domain.Loans;
using LoanProject.Domain.Loans.Events;
using LoanProject.Infrastructure.EventStore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The serializer is the only translator between event records and the
/// ledger's (EventType, EventData) columns. Its contract: explicit registry
/// both ways — stored names are forever, code names are not, and nothing
/// outside the registry ever gets (de)serialized.
/// </summary>
public class LoanEventSerializerTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Officer = new("11111111-1111-1111-1111-111111111111");

    public static IEnumerable<object[]> AllEvents() =>
    [
        [new LoanOriginated(Guid.NewGuid(), Guid.NewGuid(), 100_000m, 0.12m, RateType.Effective, 12, Now)],
        [new LoanApproved(Officer, "officer-1", Now)],
        [new LoanRejected(Officer, "officer-1", "insufficient income", Now)],
        [new LoanDisbursed(100_000m, Officer, "officer-1", Now)],
        [new PaymentReceived(Guid.NewGuid(), 8_884.88m, 1, "evt_test_1", Now)],
        [new LoanSettled(Guid.NewGuid(), Now)],
        [new LoanDefaulted(91, 92_115.12m, Now)],
    ];

    [Theory]
    [MemberData(nameof(AllEvents))]
    public void RoundTrip_EveryEventType_PreservesAllValues(IDomainEvent original)
    {
        var (eventType, eventData) = LoanEventSerializer.Serialize(original);

        var replayed = LoanEventSerializer.Deserialize(eventType, eventData);

        // Records compare by value: one assert covers every property,
        // including decimal precision and the UTC timestamp.
        Assert.Equal(original, replayed);
    }

    [Fact]
    public void Serialize_KnownEvent_UsesShortStableName()
    {
        var (eventType, eventData) = LoanEventSerializer.Serialize(new LoanApproved(Officer, "officer-1", Now));

        // The stored name is the contract — short, no namespace, so code
        // can be refactored without corrupting the ledger.
        Assert.Equal("LoanApproved", eventType);
        Assert.DoesNotContain("LoanProject.Domain", eventData);
    }

    [Fact]
    public void Serialize_EnumPayload_IsStoredAsStringNotNumber()
    {
        var origination = new LoanOriginated(
            Guid.NewGuid(), Guid.NewGuid(), 100_000m, 0.12m, RateType.Effective, 12, Now);

        var (_, eventData) = LoanEventSerializer.Serialize(origination);

        // "Effective" survives enum reordering; the number 1 does not.
        Assert.Contains("\"Effective\"", eventData);
    }

    [Fact]
    public void Serialize_UnregisteredEventType_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => LoanEventSerializer.Serialize(new UnregisteredEvent(Now)));
    }

    [Fact]
    public void Deserialize_UnknownEventType_Throws()
    {
        // A foreign or corrupted row must fail fast, never guess a type.
        Assert.Throws<NotSupportedException>(
            () => LoanEventSerializer.Deserialize("NotARealEvent", "{}"));
    }

    [Fact]
    public void Registry_CoversEveryConcreteDomainEvent()
    {
        // Guard for schema evolution: adding an event record in Domain and
        // forgetting the registry must break the build, loudly, right here.
        var concreteEvents = typeof(IDomainEvent).Assembly.GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToHashSet();

        Assert.Equal(concreteEvents, LoanEventSerializer.RegisteredEventTypes.ToHashSet());
    }

    private sealed record UnregisteredEvent(DateTime OccurredAtUtc) : IDomainEvent;
}
