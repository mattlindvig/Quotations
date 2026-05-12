using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

public class AiBatchJobRepository : IAiBatchJobRepository
{
    private readonly IMongoCollection<AiBatchJob> _collection;

    public AiBatchJobRepository(MongoDbService mongoDbService)
    {
        _collection = mongoDbService.GetCollection<AiBatchJob>("ai_batch_jobs");
    }

    public async Task<AiBatchJob> CreateAsync(AiBatchJob job)
    {
        await _collection.InsertOneAsync(job);
        return job;
    }

    public async Task<AiBatchJob?> GetByIdAsync(string id)
    {
        return await _collection.Find(j => j.Id == id).FirstOrDefaultAsync();
    }

    public async Task<AiBatchJob?> GetByAnthropicBatchIdAsync(string anthropicBatchId)
    {
        return await _collection.Find(j => j.AnthropicBatchId == anthropicBatchId).FirstOrDefaultAsync();
    }

    public async Task<List<AiBatchJob>> GetPendingJobsAsync()
    {
        var filter = Builders<AiBatchJob>.Filter.In(j => j.Status,
            new[] { AiBatchJobStatus.Submitted, AiBatchJobStatus.InProgress });
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<bool> UpdateAsync(AiBatchJob job)
    {
        var result = await _collection.ReplaceOneAsync(j => j.Id == job.Id, job);
        return result.ModifiedCount > 0;
    }

    public async Task<List<AiBatchJob>> GetRecentAsync(int limit = 20)
    {
        return await _collection
            .Find(FilterDefinition<AiBatchJob>.Empty)
            .SortByDescending(j => j.SubmittedAt)
            .Limit(limit)
            .ToListAsync();
    }
}
