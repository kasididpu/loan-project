using System.Text.Json;
using System.Text.Json.Serialization;
using LoanProject.Domain.Loans.Events;

namespace LoanProject.Infrastructure.EventStore;

/// <summary>
/// Translates Loan domain events to and from the ledger columns
/// (EventType, EventData). Stored names are a permanent contract decoupled
/// from CLR type names; only registered types are ever (de)serialized —
/// the serializer never resolves a type from data.
/// </summary>
public static class LoanEventSerializer
{
    // The stored name is forever: code can be renamed or moved freely as
    // long as this map keeps pointing the old name at the right type.
    // Schema evolution = add a new name here, never rewrite old rows.
    private static readonly Dictionary<string, Type> ByStoredName = new()
    {
        ["LoanOriginated"] = typeof(LoanOriginated),
        ["LoanApproved"] = typeof(LoanApproved),
        ["LoanRejected"] = typeof(LoanRejected),
        ["LoanDisbursed"] = typeof(LoanDisbursed),
        ["PaymentReceived"] = typeof(PaymentReceived),
        ["LoanSettled"] = typeof(LoanSettled),
        ["LoanDefaulted"] = typeof(LoanDefaulted),
    };

    private static readonly Dictionary<Type, string> ByClrType =
        ByStoredName.ToDictionary(pair => pair.Value, pair => pair.Key);

    // Enums are stored as strings: "Effective" survives a reordered enum,
    // the number 1 does not — same fragility class as CLR type names.
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyCollection<Type> RegisteredEventTypes => ByClrType.Keys;

    public static (string EventType, string EventData) Serialize(IDomainEvent domainEvent)
    {
        if (!ByClrType.TryGetValue(domainEvent.GetType(), out var storedName))
            throw new NotSupportedException(
                $"Event type '{domainEvent.GetType().Name}' is not registered in the event serializer.");

        // Serialize as the concrete type — serializing as IDomainEvent would
        // write only the interface's members.
        var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Options);
        return (storedName, json);
    }

    public static IDomainEvent Deserialize(string eventType, string eventData)
    {
        if (!ByStoredName.TryGetValue(eventType, out var clrType))
            throw new NotSupportedException(
                $"Unknown event type '{eventType}' in the event store.");

        return (IDomainEvent)(JsonSerializer.Deserialize(eventData, clrType, Options)
            ?? throw new InvalidOperationException(
                $"Event data for '{eventType}' deserialized to null."));
    }
}
