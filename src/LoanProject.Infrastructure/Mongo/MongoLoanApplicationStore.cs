using LoanProject.Application.LoanApplications;
using MongoDB.Driver;

namespace LoanProject.Infrastructure.Mongo;

public sealed class MongoLoanApplicationStore : ILoanApplicationStore
{
    private const string CollectionName = "loanApplications";

    private readonly IMongoCollection<LoanApplicationDocument> _collection;

    static MongoLoanApplicationStore() => MongoDefaults.EnsureConfigured();

    public MongoLoanApplicationStore(IMongoDatabase database) =>
        _collection = database.GetCollection<LoanApplicationDocument>(CollectionName);

    // Upsert by id: the Id property maps to Mongo's _id by convention, so
    // resubmitting an application replaces the whole document.
    public Task SaveAsync(LoanApplicationDocument document, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(
            existing => existing.Id == document.Id,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    public async Task<LoanApplicationDocument?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _collection
            .Find(document => document.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
}
