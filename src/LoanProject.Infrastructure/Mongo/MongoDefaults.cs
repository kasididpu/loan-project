using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace LoanProject.Infrastructure.Mongo;

internal static class MongoDefaults
{
    /// <summary>
    /// Driver 3.x ships with GuidRepresentation Unspecified and throws on
    /// any Guid until told which binary subtype to use. Standard (subtype 4)
    /// is byte-order-consistent across all driver languages — the legacy
    /// subtype 3 famously was not. Idempotent; call from every Mongo class's
    /// static constructor.
    /// </summary>
    public static void EnsureConfigured() =>
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
}
