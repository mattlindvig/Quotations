using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;

namespace Quotations.Api.Repositories;

public class FavQsSyncRepository : IFavQsSyncRepository
{
    private readonly IMongoCollection<FavQsSyncRecord> _collection;

    public FavQsSyncRepository(MongoDbService db)
    {
        _collection = db.GetDatabase().GetCollection<FavQsSyncRecord>("favqsSyncs");
    }

    public async Task<FavQsSyncRecord?> GetLastCompletedAsync() =>
        await _collection
            .Find(Builders<FavQsSyncRecord>.Filter.Eq(r => r.Status, FavQsSyncStatus.Completed))
            .SortByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync();

    public async Task<FavQsSyncRecord?> GetLastAsync() =>
        await _collection
            .Find(Builders<FavQsSyncRecord>.Filter.Empty)
            .SortByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

    public async Task<FavQsSyncRecord?> GetRunningAsync() =>
        await _collection
            .Find(Builders<FavQsSyncRecord>.Filter.Eq(r => r.Status, FavQsSyncStatus.Running))
            .FirstOrDefaultAsync();

    public async Task<FavQsSyncRecord> CreateAsync(FavQsSyncRecord record)
    {
        await _collection.InsertOneAsync(record);
        return record;
    }

    public async Task UpdateAsync(FavQsSyncRecord record) =>
        await _collection.ReplaceOneAsync(
            Builders<FavQsSyncRecord>.Filter.Eq(r => r.Id, record.Id),
            record);

    public async Task<List<FavQsSyncRecord>> GetRecentAsync(int limit = 10) =>
        await _collection
            .Find(Builders<FavQsSyncRecord>.Filter.Empty)
            .SortByDescending(r => r.StartedAt)
            .Limit(limit)
            .ToListAsync();
}
