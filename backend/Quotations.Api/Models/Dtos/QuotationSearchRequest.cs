using System.Collections.Generic;

namespace Quotations.Api.Models.Dtos;

/// <summary>
/// Request DTO for quotation search operations
/// </summary>
public class QuotationSearchRequest
{
    /// <summary>
    /// Search query text (searches in quotation text, author name, source title)
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Filter by author ID
    /// </summary>
    public string? AuthorId { get; set; }

    /// <summary>
    /// Filter by source type (book, movie, speech, interview, other)
    /// </summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// Filter by tags (comma-separated list)
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Items per page (max 100)
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Filter by status (pending, approved, rejected)
    /// </summary>
    public string? Status { get; set; }
}
