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
        List<string>? tags = null);

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
        QuotationStatus? status = null);

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
    /// Check if a duplicate quotation exists (same text, author, source)
    /// </summary>
    /// <param name="text">Quotation text</param>
    /// <param name="authorId">Author ID</param>
    /// <param name="sourceId">Source ID</param>
    /// <returns>True if duplicate exists</returns>
    Task<bool> IsDuplicateAsync(string text, string authorId, string sourceId);

    /// <summary>
    /// Get distinct tags with usage counts
    /// </summary>
    /// <param name="limit">Maximum number of tags to return (optional)</param>
    /// <returns>List of tags with their usage counts</returns>
    Task<List<(string Tag, int Count)>> GetTagsWithCountsAsync(int? limit = null, string? authorName = null, SourceType? sourceType = null);

    /// <summary>
    /// Find potential duplicate quotations
    /// </summary>
    Task<List<Quotation>> FindPotentialDuplicatesAsync(string text, string authorId, string sourceId, string excludeId);

    /// <summary>
    /// Get a batch of quotations whose AI review is pending or not yet started
    /// </summary>
    Task<List<Quotation>> GetPendingAiReviewsAsync(int batchSize);

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
}
