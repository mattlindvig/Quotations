using MongoDB.Bson;
using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

/// <summary>
/// MongoDB implementation of source repository
/// </summary>
public class SourceRepository : ISourceRepository
{
    private readonly IMongoCollection<Source> _sources;

    public SourceRepository(MongoDbService mongoDbService)
    {
        _sources = mongoDbService.GetCollection<Source>("sources");
    }

    public async Task<List<Source>> GetSourcesAsync(SourceType? type = null, int? limit = null)
    {
        var filterBuilder = Builders<Source>.Filter;
        var filter = type.HasValue
            ? filterBuilder.Eq(s => s.Type, type.Value)
            : FilterDefinition<Source>.Empty;

        IFindFluent<Source, Source> query = _sources
            .Find(filter)
            .SortBy(s => s.Title);

        if (limit.HasValue)
        {
            query = query.Limit(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Source?> GetSourceByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _sources
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Source?> GetSourceByTitleAndTypeAsync(string title, SourceType type)
    {
        return await _sources
            .Find(s => s.Title.ToLower() == title.ToLower() && s.Type == type)
            .FirstOrDefaultAsync();
    }

    public async Task<Source> CreateSourceAsync(Source source)
    {
        source.CreatedAt = DateTime.UtcNow;
        source.UpdatedAt = DateTime.UtcNow;

        await _sources.InsertOneAsync(source);
        return source;
    }

    public async Task<bool> UpdateSourceAsync(Source source)
    {
        source.UpdatedAt = DateTime.UtcNow;

        var result = await _sources.ReplaceOneAsync(
            s => s.Id == source.Id,
            source);

        return result.ModifiedCount > 0;
    }
}
