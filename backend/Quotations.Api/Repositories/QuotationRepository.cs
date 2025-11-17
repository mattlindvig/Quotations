using MongoDB.Bson;
using MongoDB.Driver;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        // Apply author filter
        if (!string.IsNullOrEmpty(authorId))
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

    public async Task<List<(string Tag, int Count)>> GetTagsWithCountsAsync(int? limit = null)
    {
        var pipelineStages = new List<BsonDocument>
        {
            // Match only approved quotations
            new BsonDocument("$match", new BsonDocument("status", "Approved")),
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
