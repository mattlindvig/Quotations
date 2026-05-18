using Quotations.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Repositories;

/// <summary>
/// Repository interface for quotation data access
/// </summary>
public interface IQuotationRepository
{
    /// <summary>
    /// Get paginated list of quotations with optional filters
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="authorId">Filter by author ID (optional)</param>
    /// <param name="sourceType">Filter by source type (optional)</param>
    /// <param name="tags">Filter by tags (optional)</param>
    /// <returns>List of quotations and total count</returns>
    Task<(List<Quotation> Items, long TotalCount)> GetQuotationsAsync(
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
        int? yearTo = null);

    /// <summary>
    /// Get distinct author names from quotations, ordered by usage count descending
    /// </summary>
    Task<List<string>> GetDistinctAuthorNamesAsync(int limit = 500);

    /// <summary>
    /// Get a single quotation by ID
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <returns>Quotation or null if not found</returns>
    Task<Quotation?> GetQuotationByIdAsync(string id);

    /// <summary>
    /// Search quotations by text (full-text search)
    /// </summary>
    /// <param name="searchText">Search query</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <returns>List of matching quotations and total count</returns>
    Task<(List<Quotation> Items, long TotalCount)> SearchQuotationsAsync(
        string searchText,
        int page = 1,
        int pageSize = 20,
        QuotationStatus? status = null,
        string? authorName = null,
        SourceType? sourceType = null,
        List<string>? tags = null,
        int? yearFrom = null,
        int? yearTo = null);

    /// <summary>
    /// Create a new quotation
    /// </summary>
    /// <param name="quotation">Quotation to create</param>
    /// <returns>Created quotation with ID</returns>
    Task<Quotation> CreateQuotationAsync(Quotation quotation);

    /// <summary>
    /// Update an existing quotation
    /// </summary>
    /// <param name="quotation">Quotation to update</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateQuotationAsync(Quotation quotation);

    /// <summary>
    /// Delete a quotation by ID
    /// </summary>
    /// <param name="id">Quotation ID</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteQuotationAsync(string id);

    /// <summary>
    /// Check if an exact duplicate quotation exists (same normalized text, any author/source).
    /// </summary>
    Task<bool> IsDuplicateAsync(string text);

    /// <summary>
    /// Get distinct tags with usage counts
    /// </summary>
    /// <param name="limit">Maximum number of tags to return (optional)</param>
    /// <returns>List of tags with their usage counts</returns>
    Task<List<(string Tag, int Count)>> GetTagsWithCountsAsync(int? limit = null, string? authorName = null, SourceType? sourceType = null);

    /// <summary>
    /// Find potential duplicate quotations by text similarity, optionally narrowed to the same author name.
    /// </summary>
    Task<List<Quotation>> FindPotentialDuplicatesAsync(string text, string authorName, string excludeId);

    /// <summary>
    /// Get a batch of quotations whose AI review is pending or not yet started
    /// </summary>
    Task<List<Quotation>> GetPendingAiReviewsAsync(int batchSize);

    /// <summary>
    /// Get paginated quotations that have not yet been AI-reviewed (status = NotReviewed)
    /// </summary>
    Task<(List<Quotation> Items, long TotalCount)> GetUnreviewedForAiAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Get counts of quotations grouped by aiReview.status, plus average accuracy scores
    /// </summary>
    Task<Dictionary<string, long>> GetAiReviewCountsByStatusAsync();

    /// <summary>
    /// Get average AI accuracy scores across all reviewed quotations
    /// </summary>
    Task<(double? QuoteAccuracy, double? Attribution, double? Source)> GetAverageAiScoresAsync();

    /// <summary>
    /// Get most recently AI-reviewed quotations
    /// </summary>
    Task<List<Quotation>> GetRecentlyAiReviewedAsync(int limit = 20);

    /// <summary>
    /// Update only the aiReview subdocument of a quotation, leaving all other fields untouched.
    /// Use this instead of UpdateQuotationAsync when only AI review data changes, to avoid
    /// ObjectId serialization errors on quotations with empty author/source IDs.
    /// </summary>
    Task<bool> UpdateAiReviewAsync(string quotationId, AiReview aiReview);

    /// <summary>
    /// Reset AI review state to NotReviewed so the background service picks it up again
    /// </summary>
    Task<bool> ResetAiReviewAsync(string quotationId);

    /// <summary>
    /// Reset AI review state for all Failed quotations
    /// </summary>
    Task<long> ResetAllFailedAiReviewsAsync();

    /// <summary>
    /// Return a single random approved quotation
    /// </summary>
    Task<Quotation?> GetRandomQuotationAsync();

    /// <summary>
    /// Full-text search using the MongoDB text index (faster than regex on large collections).
    /// Intended for the chat service where queries are always complete words.
    /// </summary>
    Task<List<Quotation>> TextSearchAsync(string searchText, int limit = 5, QuotationStatus status = QuotationStatus.Approved);

    /// <summary>
    /// Return a batch of random approved quotations with optional source type and tag filters.
    /// </summary>
    Task<List<Quotation>> GetRandomBatchAsync(int count, SourceType? sourceType = null, List<string>? tags = null);

    /// <summary>
    /// Return a single random approved quotation, excluding the given IDs (used for QOTD deduplication).
    /// </summary>
    Task<Quotation?> GetRandomExcludingAsync(IEnumerable<string> excludeIds);

    /// <summary>
    /// Get quotations by a list of IDs, preserving insertion order
    /// </summary>
    Task<List<Quotation>> GetByIdsAsync(IEnumerable<string> ids);

    /// <summary>
    /// Get paginated quotations submitted by a specific user
    /// </summary>
    Task<(List<Quotation> Items, long TotalCount)> GetBySubmitterIdAsync(string userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Bulk-update aiReview.status for a list of quotation IDs in a single MongoDB operation.
    /// Used by the Batch API to mark thousands of quotations as BatchPending in one call.
    /// </summary>
    Task<long> BulkSetAiReviewStatusAsync(IEnumerable<string> quotationIds, AiReviewStatus status);

    /// <summary>
    /// Get up to <paramref name="limit"/> NotReviewed quotations for batch submission.
    /// </summary>
    Task<List<Quotation>> GetUnreviewedForBatchAsync(int limit);

    /// <summary>
    /// Get up to <paramref name="limit"/> FixPending quotations for fix batch submission.
    /// </summary>
    Task<List<Quotation>> GetFixPendingForBatchAsync(int limit);
}
