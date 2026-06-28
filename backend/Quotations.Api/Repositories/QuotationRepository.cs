using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Quotations.Api.Models;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiReviewStatusEnum = Quotations.Api.Models.AiReviewStatus;

namespace Quotations.Api.Repositories;

public class QuotationRepository : IQuotationRepository
{
    private readonly IMongoCollection<Quotation> _quotations;
    private readonly MeilisearchService _meilisearch;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QuotationRepository> _logger;

    private static readonly TimeSpan AuthorCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan TagCacheTtl    = TimeSpan.FromHours(1);
    private static readonly TimeSpan CountCacheTtl  = TimeSpan.FromMinutes(5);

    public QuotationRepository(MongoDbService mongoDbService, MeilisearchService meilisearch, IMemoryCache cache, ILogger<QuotationRepository> logger)
    {
        _quotations = mongoDbService.GetCollection<Quotation>("quotations");
        _meilisearch = meilisearch;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(List<Quotation> Items, long TotalCount)> GetQuotationsAsync(
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null,
        string? authorId = null,
        string? authorName = null,
        SourceType? sourceType = null,
        string? sourceTitle = null,
        List<string>? tags = null,
        string? sortBy = null,
        int? yearFrom = null,
        int? yearTo = null)
    {
        var effectiveStatus = status ?? QuotationStatus.Approved;

        // Route filter-browse through Meilisearch when any filter beyond status is present.
        // Meilisearch only holds Approved docs, so fall back to MongoDB for other statuses.
        bool hasFilter = !string.IsNullOrEmpty(authorName) ||
                         !string.IsNullOrEmpty(sourceTitle) ||
                         sourceType.HasValue ||
                         (tags != null && tags.Any()) ||
                         yearFrom.HasValue || yearTo.HasValue;

        if (_meilisearch.Enabled && effectiveStatus == QuotationStatus.Approved && hasFilter)
        {
            try
            {
                var (hits, total) = await _meilisearch.SearchAsync(
                    "", page, pageSize,
                    status: effectiveStatus.ToString(),
                    authorName: authorName,
                    sourceType: sourceType?.ToString(),
                    sourceTitle: sourceTitle,
                    tags: tags,
                    yearFrom: yearFrom,
                    yearTo: yearTo,
                    sortBy: sortBy ?? "newest");

                if (hits.Count == 0)
                    return (new List<Quotation>(), total);

                return (hits.Select(ToQuotation).ToList(), total);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meilisearch filter-browse failed, falling back to MongoDB");
            }
        }

        var filterBuilder = Builders<Quotation>.Filter;
        var filters = new List<FilterDefinition<Quotation>>
        {
            filterBuilder.Eq(q => q.Status, effectiveStatus)
        };

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
            filters.Add(filterBuilder.Eq(q => q.Source.Type, sourceType.Value));

        // Apply source title filter
        if (!string.IsNullOrEmpty(sourceTitle))
            filters.Add(filterBuilder.Eq(q => q.Source.Title, sourceTitle));

        // Apply tags filter (quotation must have all specified tags)
        if (tags != null && tags.Any())
            filters.Add(filterBuilder.All(q => q.Tags, tags));

        // Apply year range filters
        if (yearFrom.HasValue)
            filters.Add(filterBuilder.Gte(q => q.Source.Year, yearFrom.Value));
        if (yearTo.HasValue)
            filters.Add(filterBuilder.Lte(q => q.Source.Year, yearTo.Value));

        var combinedFilter = filterBuilder.And(filters);

        var totalCount = await _quotations.CountDocumentsAsync(combinedFilter);

        var sortDefinition = sortBy switch
        {
            "oldest" => Builders<Quotation>.Sort.Ascending(q => q.SubmittedAt),
            "author" => Builders<Quotation>.Sort.Ascending(q => q.Author.Name),
            "year" => Builders<Quotation>.Sort.Descending("source.year"),
            _ => Builders<Quotation>.Sort.Descending(q => q.SubmittedAt),
        };

        var items = await _quotations
            .Find(combinedFilter)
            .Sort(sortDefinition)
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
        QuotationStatus? status = null,
        string? authorName = null,
        SourceType? sourceType = null,
        List<string>? tags = null,
        int? yearFrom = null,
        int? yearTo = null)
    {
        var effectiveStatus = status ?? QuotationStatus.Approved;

        var (hits, total) = await _meilisearch.SearchAsync(
            searchText, page, pageSize,
            status: effectiveStatus.ToString(),
            authorName: authorName,
            sourceType: sourceType?.ToString(),
            tags: tags,
            yearFrom: yearFrom,
            yearTo: yearTo);

        return (hits.Select(ToQuotation).ToList(), total);
    }

    public async Task<Quotation> CreateQuotationAsync(Quotation quotation)
    {
        quotation.CreatedAt = DateTime.UtcNow;
        quotation.UpdatedAt = DateTime.UtcNow;
        quotation.SubmittedAt = DateTime.UtcNow;
        quotation.TextHash ??= Quotation.ComputeTextHash(quotation.Text);

        await _quotations.InsertOneAsync(quotation);
        return quotation;
    }

    public async Task<bool> UpdateQuotationAsync(Quotation quotation)
    {
        quotation.UpdatedAt = DateTime.UtcNow;

        var result = await _quotations.ReplaceOneAsync(
            q => q.Id == quotation.Id,
            quotation);

        if (result.ModifiedCount > 0 && _meilisearch.Enabled)
        {
            FireAndForgetMeili(quotation.Status == QuotationStatus.Approved
                ? _meilisearch.IndexDocumentsAsync(new[] { ToMeiliDoc(quotation) })
                : _meilisearch.DeleteDocumentAsync(quotation.Id));
        }

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteQuotationAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _))
            return false;

        var result = await _quotations.DeleteOneAsync(q => q.Id == id);

        if (result.DeletedCount > 0 && _meilisearch.Enabled)
            FireAndForgetMeili(_meilisearch.DeleteDocumentAsync(id));

        return result.DeletedCount > 0;
    }

    public async Task<bool> IsDuplicateAsync(string text)
    {
        var normalizedText = text.Trim();
        var filter = Builders<Quotation>.Filter.And(
            Builders<Quotation>.Filter.Regex(q => q.Text, new BsonRegularExpression($"^{Regex.Escape(normalizedText)}$", "i")),
            Builders<Quotation>.Filter.Ne(q => q.Status, QuotationStatus.Rejected)
        );

        var count = await _quotations.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<List<(string Tag, int Count)>> GetTagsWithCountsAsync(int? limit = null, string? authorName = null, SourceType? sourceType = null, int? maxCount = null)
    {
        // Cache only the unfiltered default call — filtered calls are infrequent and vary widely
        bool isDefaultCall = string.IsNullOrEmpty(authorName) && !sourceType.HasValue && !maxCount.HasValue;
        var cacheKey = $"tags:{limit}";
        if (isDefaultCall && _cache.TryGetValue(cacheKey, out List<(string, int)>? cachedTags) && cachedTags != null)
            return cachedTags;

        var matchDoc = new BsonDocument("status", "Approved");
        if (!string.IsNullOrEmpty(authorName))
            matchDoc.Add("author.name", authorName);
        if (sourceType.HasValue)
            matchDoc.Add("source.type", sourceType.Value.ToString().ToLowerInvariant());

        var pipelineStages = new List<BsonDocument>
        {
            new BsonDocument("$match", matchDoc),
            new BsonDocument("$unwind", "$tags"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$tags" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        // Drop tags that are so broad they don't usefully narrow results
        if (maxCount.HasValue)
        {
            pipelineStages.Add(new BsonDocument("$match",
                new BsonDocument("count", new BsonDocument("$lte", maxCount.Value))));
        }

        if (limit.HasValue)
        {
            pipelineStages.Add(new BsonDocument("$limit", limit.Value));
        }

        var results = await _quotations.Aggregate<BsonDocument>(pipelineStages).ToListAsync();

        var tags = results.Select(doc => (
            Tag: doc["_id"].AsString,
            Count: doc["count"].AsInt32
        )).ToList();

        if (isDefaultCall)
            _cache.Set(cacheKey, tags, TagCacheTtl);

        return tags;
    }

    /// <summary>
    /// Find potential duplicate quotations based on text similarity, author, and source
    /// </summary>
    public async Task<List<Quotation>> FindPotentialDuplicatesAsync(string text, string authorName, string excludeId)
    {
        var normalizedText = NormalizeForComparison(text);

        var filters = new List<FilterDefinition<Quotation>>
        {
            Builders<Quotation>.Filter.Ne(q => q.Status, QuotationStatus.Rejected),
        };

        // Exclude the quotation itself when an ID is provided
        if (!string.IsNullOrWhiteSpace(excludeId))
            filters.Add(Builders<Quotation>.Filter.Ne(q => q.Id, excludeId));

        // Narrow by author name when provided (not ID — imported quotes have empty IDs)
        if (!string.IsNullOrWhiteSpace(authorName))
            filters.Add(Builders<Quotation>.Filter.Eq("author.name", authorName));

        var candidates = await _quotations
            .Find(Builders<Quotation>.Filter.And(filters))
            .Limit(200)
            .ToListAsync();

        return candidates.Where(q =>
        {
            var candidateNormalized = NormalizeForComparison(q.Text);

            if (candidateNormalized == normalizedText) return true;

            if (normalizedText.Contains(candidateNormalized) || candidateNormalized.Contains(normalizedText))
                return true;

            // 80% Levenshtein similarity — catches paraphrasing and minor wording differences
            if (CalculateSimilarity(normalizedText, candidateNormalized) >= 0.80)
                return true;

            return false;
        }).ToList();
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

    public async Task<(List<Quotation> Items, long TotalCount)> GetUnreviewedForAiAsync(int page = 1, int pageSize = 20)
    {
        var filter = Builders<Quotation>.Filter.Eq("aiReview.status", nameof(AiReviewStatusEnum.NotReviewed));

        var total = await _quotations.CountDocumentsAsync(filter);
        var items = await _quotations
            .Find(filter)
            .SortBy(q => q.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
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
        var filter = Builders<Quotation>.Filter.Eq(q => q.Status, QuotationStatus.Approved);
        var count = await GetApprovedCountAsync(filter);
        if (count == 0) return null;

        var skip = (int)Random.Shared.NextInt64(0, count);
        return await _quotations.Find(filter).Skip(skip).Limit(1).FirstOrDefaultAsync();
    }

    private async Task<long> GetApprovedCountAsync(FilterDefinition<Quotation> filter)
    {
        const string key = "count:Approved";
        if (_cache.TryGetValue(key, out long cached))
            return cached;

        var count = await _quotations.CountDocumentsAsync(filter);
        _cache.Set(key, count, CountCacheTtl);
        return count;
    }

    public async Task<bool> UpdateAiReviewAsync(string quotationId, AiReview aiReview)
    {
        var update = Builders<Quotation>.Update
            .Set(q => q.AiReview, aiReview)
            .Set(q => q.UpdatedAt, DateTime.UtcNow);

        var result = await _quotations.UpdateOneAsync(q => q.Id == quotationId, update);
        return result.ModifiedCount > 0;
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
        var cacheKey = $"authors:{limit}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
            return cached;

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
        var names = results
            .Where(doc => doc.Contains("name") && doc["name"] != BsonNull.Value)
            .Select(doc => doc["name"].AsString)
            .ToList();

        _cache.Set(cacheKey, names, AuthorCacheTtl);
        return names;
    }

    /// <summary>
    /// Strips punctuation and collapses whitespace so comparison focuses on words.
    /// </summary>
    private static string NormalizeForComparison(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasSpace = true;
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Levenshtein-based similarity ratio (0.0–1.0). Uses two-row rolling array
    /// to keep memory O(n) instead of O(m*n).
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        int[] prev = new int[s2.Length + 1];
        int[] curr = new int[s2.Length + 1];

        for (int j = 0; j <= s2.Length; j++) prev[j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        int distance = prev[s2.Length];
        return 1.0 - (double)distance / Math.Max(s1.Length, s2.Length);
    }

    public async Task<List<Quotation>> TextSearchAsync(
        string searchText,
        int limit = 5,
        QuotationStatus status = QuotationStatus.Approved)
    {
        if (_meilisearch.Enabled && !string.IsNullOrWhiteSpace(searchText))
        {
            try
            {
                var (hits, _) = await _meilisearch.SearchAsync(
                    searchText, page: 1, pageSize: limit, status: status.ToString());
                if (hits.Count > 0)
                    return hits.Select(ToQuotation).ToList();
            }
            catch { }
        }

        try
        {
            var filter = Builders<Quotation>.Filter.And(
                Builders<Quotation>.Filter.Text(searchText),
                Builders<Quotation>.Filter.Eq(q => q.Status, status)
            );

            return await _quotations.Find(filter).Limit(limit).ToListAsync();
        }
        catch (MongoCommandException)
        {
            return new List<Quotation>();
        }
    }

    private static Quotation ToQuotation(MeiliQuotationDoc doc) => new()
    {
        Id = doc.Id,
        Text = doc.Text,
        Author = new AuthorReference { Name = doc.AuthorName },
        Source = new SourceReference
        {
            Title = doc.SourceTitle,
            Type = Enum.TryParse<SourceType>(doc.SourceType, true, out var st) ? st : SourceType.Other,
            Year = doc.Year,
        },
        Tags = doc.Tags,
        Status = QuotationStatus.Approved,
        SubmittedAt = DateTimeOffset.FromUnixTimeSeconds(doc.SubmittedAt).UtcDateTime,
    };

    private static MeiliQuotationDoc ToMeiliDoc(Quotation q) => new()
    {
        Id = q.Id,
        Text = q.Text ?? "",
        AuthorName = q.Author?.Name ?? "",
        SourceTitle = q.Source?.Title ?? "",
        SourceType = q.Source?.Type.ToString() ?? "Other",
        Status = q.Status.ToString(),
        Tags = q.Tags ?? new(),
        Year = q.Source?.Year,
        SubmittedAt = new DateTimeOffset(DateTime.SpecifyKind(q.SubmittedAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
    };

    private void FireAndForgetMeili(Task task) =>
        task.ContinueWith(
            t => _logger.LogWarning(t.Exception, "Background Meilisearch sync failed"),
            TaskContinuationOptions.OnlyOnFaulted);

    public async Task<List<Quotation>> GetRandomBatchAsync(
        int count,
        SourceType? sourceType = null,
        List<string>? tags = null)
    {
        var filterBuilder = Builders<Quotation>.Filter;
        var filters = new List<FilterDefinition<Quotation>>
        {
            filterBuilder.Eq(q => q.Status, QuotationStatus.Approved)
        };

        if (sourceType.HasValue)
            filters.Add(filterBuilder.Eq(q => q.Source.Type, sourceType.Value));

        if (tags != null && tags.Count > 0)
            filters.Add(filterBuilder.All(q => q.Tags, tags));

        var pipeline = new EmptyPipelineDefinition<Quotation>()
            .Match(filterBuilder.And(filters))
            .AppendStage<Quotation, Quotation, Quotation>(
                new BsonDocument("$sample", new BsonDocument("size", count)));

        return await _quotations.Aggregate(pipeline).ToListAsync();
    }

    public async Task<Quotation?> GetRandomExcludingAsync(IEnumerable<string> excludeIds)
    {
        var excludeList = excludeIds.ToList();
        var filter = excludeList.Count > 0
            ? Builders<Quotation>.Filter.And(
                Builders<Quotation>.Filter.Eq(q => q.Status, QuotationStatus.Approved),
                Builders<Quotation>.Filter.Nin(q => q.Id, excludeList))
            : Builders<Quotation>.Filter.Eq(q => q.Status, QuotationStatus.Approved);

        var count = excludeList.Count > 0
            ? await _quotations.CountDocumentsAsync(filter)
            : await GetApprovedCountAsync(filter);
        if (count == 0) return null;

        var skip = (int)Random.Shared.NextInt64(0, count);
        return await _quotations.Find(filter).Skip(skip).Limit(1).FirstOrDefaultAsync();
    }


    public async Task<List<Quotation>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new List<Quotation>();

        var filter = Builders<Quotation>.Filter.In(q => q.Id, idList);
        var quotations = await _quotations.Find(filter).ToListAsync();

        // Preserve the order of the input IDs
        var orderMap = idList.Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        return quotations
            .OrderBy(q => orderMap.TryGetValue(q.Id, out var i) ? i : int.MaxValue)
            .ToList();
    }

    public async Task<(List<Quotation> Items, long TotalCount)> GetBySubmitterIdAsync(string userId, int page = 1, int pageSize = 20)
    {
        var filter = Builders<Quotation>.Filter.Eq("submittedBy.id", userId);
        var total = await _quotations.CountDocumentsAsync(filter);
        var items = await _quotations
            .Find(filter)
            .SortByDescending(q => q.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<long> BulkSetAiReviewStatusAsync(IEnumerable<string> quotationIds, AiReviewStatusEnum status)
    {
        var ids = quotationIds.ToList();
        if (ids.Count == 0) return 0;

        var filter = Builders<Quotation>.Filter.In(q => q.Id, ids);
        var update = Builders<Quotation>.Update.Set("aiReview.status", status.ToString());
        var result = await _quotations.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }

    public async Task<long> BulkDeleteByAiStatusAsync(AiReviewStatusEnum status)
    {
        var filter = Builders<Quotation>.Filter.Eq("aiReview.status", status.ToString());
        var result = await _quotations.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    public async Task<List<Quotation>> GetUnreviewedForBatchAsync(int limit)
    {
        var filter = Builders<Quotation>.Filter.Or(
            Builders<Quotation>.Filter.Eq("aiReview.status", AiReviewStatusEnum.NotReviewed.ToString()),
            Builders<Quotation>.Filter.Exists("aiReview", false)
        );

        return await _quotations
            .Find(filter)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<Quotation>> GetFixPendingForBatchAsync(int limit)
    {
        var filter = Builders<Quotation>.Filter.Eq("aiReview.status", AiReviewStatus.FixPending.ToString());
        return await _quotations
            .Find(filter)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<(int inserted, int skipped)> BulkInsertAsync(IEnumerable<Quotation> quotations)
    {
        var list = quotations.ToList();
        try
        {
            await _quotations.InsertManyAsync(list, new InsertManyOptions { IsOrdered = false });
            return (list.Count, 0);
        }
        catch (MongoDB.Driver.MongoBulkWriteException ex)
        {
            var skipped = ex.WriteErrors.Count(e => e.Code == 11000); // duplicate key
            return (list.Count - skipped, skipped);
        }
    }

    public async Task<HashSet<string>> GetExistingTextsAsync(IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        var filter = Builders<Quotation>.Filter.In(q => q.Text, textList);
        var existing = await _quotations
            .Find(filter)
            .Project(q => q.Text)
            .ToListAsync();
        return existing.ToHashSet();
    }
}
