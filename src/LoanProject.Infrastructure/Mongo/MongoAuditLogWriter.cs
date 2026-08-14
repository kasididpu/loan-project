using LoanProject.Application.Audit;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LoanProject.Infrastructure.Mongo;

public sealed class MongoAuditLogWriter : IAuditLogWriter
{
    private const string CollectionName = "auditLog";

    private readonly IMongoCollection<AuditEntry> _collection;

    static MongoAuditLogWriter()
    {
        MongoDefaults.EnsureConfigured();

        // AuditEntry deliberately has no Id property: MongoDB assigns _id on
        // insert, and the map must ignore that element on the way back out.
        BsonClassMap.TryRegisterClassMap<AuditEntry>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });
    }

    public MongoAuditLogWriter(IMongoDatabase database) =>
        _collection = database.GetCollection<AuditEntry>(CollectionName);

    // No unit of work here: an audit fact is one self-contained document,
    // written the moment it happens — nothing to batch, nothing to track.
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(entry, options: null, cancellationToken);

    public async Task<IReadOnlyList<AuditEntry>> ListByEntityAsync(
        string entityType, string entityId, CancellationToken cancellationToken)
    {
        var byEntity = Builders<AuditEntry>.Filter.And(
            Builders<AuditEntry>.Filter.Eq(entry => entry.EntityType, entityType),
            Builders<AuditEntry>.Filter.Eq(entry => entry.EntityId, entityId));

        return await _collection
            .Find(byEntity)
            .SortBy(entry => entry.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }
}
