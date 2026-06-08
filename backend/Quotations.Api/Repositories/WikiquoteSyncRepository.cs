using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;

namespace Quotations.Api.Repositories;

public class WikiquoteSyncRepository : IWikiquoteSyncRepository
{
    private readonly IMongoCollection<WikiquoteSyncRecord> _collection;

    public WikiquoteSyncRepository(MongoDbService db)
    {
        _collection = db.GetDatabase().GetCollection<WikiquoteSyncRecord>("wikiquoteSyncs");
    }

    public async Task<WikiquoteSyncRecord?> GetLastCompletedAsync(WikiquoteSyncType? type = null)
    {
        var filter = Builders<WikiquoteSyncRecord>.Filter
            .Eq(r => r.Status, WikiquoteSyncStatus.Completed);

        if (type.HasValue)
            filter &= Builders<WikiquoteSyncRecord>.Filter.Eq(r => r.SyncType, type.Value);

        return await _collection
            .Find(filter)
            .SortByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<WikiquoteSyncRecord?> GetLastAsync(WikiquoteSyncType type) =>
        await _collection
            .Find(Builders<WikiquoteSyncRecord>.Filter.Eq(r => r.SyncType, type))
            .SortByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

    public async Task<WikiquoteSyncRecord?> GetRunningAsync() =>
        await _collection
            .Find(Builders<WikiquoteSyncRecord>.Filter.Eq(r => r.Status, WikiquoteSyncStatus.Running))
            .FirstOrDefaultAsync();

    public async Task<WikiquoteSyncRecord> CreateAsync(WikiquoteSyncRecord record)
    {
        await _collection.InsertOneAsync(record);
        return record;
    }

    public async Task UpdateAsync(WikiquoteSyncRecord record) =>
        await _collection.ReplaceOneAsync(
            Builders<WikiquoteSyncRecord>.Filter.Eq(r => r.Id, record.Id),
            record);

    public async Task<List<WikiquoteSyncRecord>> GetRecentAsync(int limit = 10) =>
        await _collection
            .Find(Builders<WikiquoteSyncRecord>.Filter.Empty)
            .SortByDescending(r => r.StartedAt)
            .Limit(limit)
            .ToListAsync();
}
