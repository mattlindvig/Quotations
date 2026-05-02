using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public class AiReviewErrorRepository : IAiReviewErrorRepository
{
    private readonly IMongoCollection<AiReviewError> _collection;

    public AiReviewErrorRepository(MongoDbService mongoDbService)
    {
        _collection = mongoDbService.GetCollection<AiReviewError>("ai_review_errors");
    }

    public async Task<AiReviewError> CreateAsync(AiReviewError error)
    {
        await _collection.InsertOneAsync(error);
        return error;
    }

    public async Task<List<AiReviewError>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        return await _collection
            .Find(FilterDefinition<AiReviewError>.Empty)
            .SortByDescending(e => e.FailedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> CountAsync()
    {
        return await _collection.CountDocumentsAsync(FilterDefinition<AiReviewError>.Empty);
    }

    public async Task<bool> DeleteByQuotationIdAsync(string quotationId)
    {
        var result = await _collection.DeleteOneAsync(e => e.QuotationId == quotationId);
        return result.DeletedCount > 0;
    }

    public async Task<long> DeleteAllAsync()
    {
        var result = await _collection.DeleteManyAsync(FilterDefinition<AiReviewError>.Empty);
        return result.DeletedCount;
    }
}
