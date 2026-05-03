using MongoDB.Bson;
using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiReviewStatusEnum = Quotations.Api.Models.AiReviewStatus;

namespace Quotations.Api.Repositories;

/// <summary>
/// MongoDB implementation of quotation repository
/// </summary>
public class QuotationRepository : IQuotationRepository
{
    private readonly IMongoCollection<Quotation> _quotations;

    public QuotationRepository(MongoDbService mongoDbService)
    {
        _quotations = mongoDbService.GetCollection<Quotation>("quotations");
    }

    public async Task<(List<Quotation> Items, long TotalCount)> GetQuotationsAsync(
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null,
        string? authorId = null,
        string? authorName = null,
        SourceType? sourceType = null,
        List<string>? tags = null)
    {
        var filterBuilder = Builders<Quotation>.Filter;
        var filters = new List<FilterDefinition<Quotation>>();

        // Apply status filter (default to approved if not specified)
        if (status.HasValue)
        {
            filters.Add(filterBuilder.Eq(q => q.Status, status.Value));
        }
        else
        {
            filters.Add(filterBuilder.Eq(q => q.Status, QuotationStatus.Approved));
        }

        // Apply author filter — prefer name (works for imported quotes with empty id)
        if (!string.IsNullOrEmpty(authorName))
        {
            filters.Add(filterBuilder.Eq(q => q.Author.Name, authorName));
        }
        else if (!string.IsNullOrEmpty(authorId))
        {
            filters.Add(filterBuilder.Eq(q => q.Author.Id, authorId));
        }

        // Apply source type filter
        if (sourceType.HasValue)
        {
            filters.Add(filterBuilder.Eq(q => q.Source.Type, sourceType.Value));
        }

        // Apply tags filter (quotation must have all specified tags)
        if (tags != null && tags.Any())
        {
            filters.Add(filterBuilder.All(q => q.Tags, tags));
        }

        var combinedFilter = filters.Any()
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        // Get total count
        var totalCount = await _quotations.CountDocumentsAsync(combinedFilter);

        // Get paginated results
        var items = await _quotations
            .Find(combinedFilter)
            .SortByDescending(q => q.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Quotation?> GetQuotationByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _quotations
            .Find(q => q.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<Quotation> Items, long TotalCount)> SearchQuotationsAsync(
        string searchText,
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null)
    {
        var filterBuilder = Builders<Quotation>.Filter;
        var filters = new List<FilterDefinition<Quotation>>();

        // Text search filter
        filters.Add(filterBuilder.Text(searchText));

        // Status filter (default to approved)
        if (status.HasValue)
        {
            filters.Add(filterBuilder.Eq(q => q.Status, status.Value));
        }
        else
        {
            filters.Add(filterBuilder.Eq(q => q.Status, QuotationStatus.Approved));
        }

        var combinedFilter = filterBuilder.And(filters);

        // Get total count
        var totalCount = await _quotations.CountDocumentsAsync(combinedFilter);

        // Get paginated results sorted by text relevance score
        var items = await _quotations
            .Find(combinedFilter)
            .Sort(Builders<Quotation>.Sort.MetaTextScore("score"))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Quotation> CreateQuotationAsync(Quotation quotation)
    {
        quotation.CreatedAt = DateTime.UtcNow;
        quotation.UpdatedAt = DateTime.UtcNow;
        quotation.SubmittedAt = DateTime.UtcNow;

        await _quotations.InsertOneAsync(quotation);
        return quotation;
    }

    public async Task<bool> UpdateQuotationAsync(Quotation quotation)
    {
        quotation.UpdatedAt = DateTime.UtcNow;

        var result = await _quotations.ReplaceOneAsync(
            q => q.Id == quotation.Id,
            quotation);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteQuotationAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return false;
        }

        var result = await _quotations.DeleteOneAsync(q => q.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> IsDuplicateAsync(string text, string authorId, string sourceId)
    {
        var normalizedText = text.Trim().ToLowerInvariant();

        var filter = Builders<Quotation>.Filter.And(
            Builders<Quotation>.Filter.Regex(q => q.Text, new BsonRegularExpression($"^{Regex.Escape(normalizedText)}$", "i")),
            Builders<Quotation>.Filter.Eq(q => q.Author.Id, authorId),
            Builders<Quotation>.Filter.Eq(q => q.Source.Id, sourceId),
            Builders<Quotation>.Filter.Eq(q => q.Status, QuotationStatus.Approved)
        );

        var count = await _quotations.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<List<(string Tag, int Count)>> GetTagsWithCountsAsync(int? limit = null, string? authorName = null, SourceType? sourceType = null)
    {
        var matchDoc = new BsonDocument("status", "Approved");
        if (!string.IsNullOrEmpty(authorName))
            matchDoc.Add("author.name", authorName);
        if (sourceType.HasValue)
            matchDoc.Add("source.type", sourceType.Value.ToString().ToLowerInvariant());

        var pipelineStages = new List<BsonDocument>
        {
            new BsonDocument("$match", matchDoc),
            // Unwind tags array
            new BsonDocument("$unwind", "$tags"),
            // Group by tag and count
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$tags" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            // Sort by count descending
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        // Add limit stage if specified
        if (limit.HasValue)
        {
            pipelineStages.Add(new BsonDocument("$limit", limit.Value));
        }

        var results = await _quotations.Aggregate<BsonDocument>(pipelineStages).ToListAsync();

        return results.Select(doc => (
            Tag: doc["_id"].AsString,
            Count: doc["count"].AsInt32
        )).ToList();
    }

    /// <summary>
    /// Find potential duplicate quotations based on text similarity, author, and source
    /// </summary>
    public async Task<List<Quotation>> FindPotentialDuplicatesAsync(string text, string authorId, string sourceId, string excludeId)
    {
        // Normalize text for comparison (trim, lowercase, remove extra whitespace)
        var normalizedText = text.Trim().ToLowerInvariant();

        var filter = Builders<Quotation>.Filter.And(
            // Exclude the quotation itself
            Builders<Quotation>.Filter.Ne(q => q.Id, excludeId),
            // Match same author
            Builders<Quotation>.Filter.Eq("author.id", authorId),
            // Match same source
            Builders<Quotation>.Filter.Eq("source.id", sourceId),
            // Only check approved or pending quotations (not rejected)
            Builders<Quotation>.Filter.In(q => q.Status, new[] { QuotationStatus.Approved, QuotationStatus.Pending })
        );

        // Get all quotations with same author and source
        var candidates = await _quotations.Find(filter).Limit(100).ToListAsync();

        // Filter by text similarity (exact match or very similar)
        var duplicates = candidates.Where(q =>
        {
            var candidateText = q.Text.Trim().ToLowerInvariant();
            // Exact match
            if (candidateText == normalizedText) return true;

            // Calculate simple similarity (Levenshtein distance would be better but this is a simple check)
            // Check if one text contains the other (handles minor differences like punctuation)
            if (normalizedText.Contains(candidateText) || candidateText.Contains(normalizedText))
                return true;

            // Check if they differ by only a few characters (typos, punctuation)
            var lengthDiff = Math.Abs(normalizedText.Length - candidateText.Length);
            if (lengthDiff <= 5 && CalculateSimilarity(normalizedText, candidateText) > 0.90)
                return true;

            return false;
        }).ToList();

        return duplicates;
    }

    public async Task<List<Quotation>> GetPendingAiReviewsAsync(int batchSize)
    {
        var filter = Builders<Quotation>.Filter.In(
            "aiReview.status",
            new[] { nameof(AiReviewStatusEnum.NotReviewed), nameof(AiReviewStatusEnum.Pending) });

        return await _quotations
            .Find(filter)
            .SortBy(q => q.SubmittedAt)
            .Limit(batchSize)
            .ToListAsync();
    }

    public async Task<Dictionary<string, long>> GetAiReviewCountsByStatusAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$aiReview.status" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var results = await _quotations.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return results
            .Where(doc => doc["_id"] != BsonNull.Value)
            .ToDictionary(
                doc => doc["_id"].AsString,
                doc => (long)doc["count"].AsInt32);
    }

    public async Task<(double? QuoteAccuracy, double? Attribution, double? Source)> GetAverageAiScoresAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("aiReview.status", "Reviewed")),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "avgQuote", new BsonDocument("$avg", "$aiReview.quoteAccuracy.score") },
                { "avgAttribution", new BsonDocument("$avg", "$aiReview.attributionAccuracy.score") },
                { "avgSource", new BsonDocument("$avg", "$aiReview.sourceAccuracy.score") }
            })
        };

        var result = await _quotations.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
        if (result == null) return (null, null, null);

        double? quote = result["avgQuote"] != BsonNull.Value ? result["avgQuote"].ToDouble() : null;
        double? attr = result["avgAttribution"] != BsonNull.Value ? result["avgAttribution"].ToDouble() : null;
        double? src = result["avgSource"] != BsonNull.Value ? result["avgSource"].ToDouble() : null;

        return (quote, attr, src);
    }

    public async Task<List<Quotation>> GetRecentlyAiReviewedAsync(int limit = 20)
    {
        var filter = Builders<Quotation>.Filter.Eq("aiReview.status", "Reviewed");

        return await _quotations
            .Find(filter)
            .SortByDescending(q => q.AiReview.ReviewedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<Quotation?> GetRandomQuotationAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("status", "Approved")),
            new BsonDocument("$sample", new BsonDocument("size", 1))
        };
        return await _quotations.Aggregate<Quotation>(pipeline).FirstOrDefaultAsync();
    }

    public async Task<bool> ResetAiReviewAsync(string quotationId)
    {
        var update = Builders<Quotation>.Update
            .Set("aiReview.status", nameof(AiReviewStatusEnum.NotReviewed))
            .Set("aiReview.retryCount", 0)
            .Set("aiReview.failureReason", BsonNull.Value)
            .Set("aiReview.lastAttemptAt", BsonNull.Value)
            .Set("updatedAt", DateTime.UtcNow);

        var result = await _quotations.UpdateOneAsync(q => q.Id == quotationId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<long> ResetAllFailedAiReviewsAsync()
    {
        var filter = Builders<Quotation>.Filter.Eq("aiReview.status", nameof(AiReviewStatusEnum.Failed));
        var update = Builders<Quotation>.Update
            .Set("aiReview.status", nameof(AiReviewStatusEnum.NotReviewed))
            .Set("aiReview.retryCount", 0)
            .Set("aiReview.failureReason", BsonNull.Value)
            .Set("aiReview.lastAttemptAt", BsonNull.Value)
            .Set("updatedAt", DateTime.UtcNow);

        var result = await _quotations.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }

    public async Task<List<string>> GetDistinctAuthorNamesAsync(int limit = 500)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("author.name", new BsonDocument("$ne", ""))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$author.name" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1)),
            new BsonDocument("$limit", limit),
            new BsonDocument("$project", new BsonDocument { { "_id", 0 }, { "name", "$_id" } })
        };

        var results = await _quotations.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return results
            .Where(doc => doc.Contains("name") && doc["name"] != BsonNull.Value)
            .Select(doc => doc["name"].AsString)
            .ToList();
    }

    /// <summary>
    /// Calculate similarity ratio between two strings (0.0 to 1.0)
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        int matches = 0;
        int minLength = Math.Min(s1.Length, s2.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (s1[i] == s2[i]) matches++;
        }

        return (double)matches / Math.Max(s1.Length, s2.Length);
    }
}
