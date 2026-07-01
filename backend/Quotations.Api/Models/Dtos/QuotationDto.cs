using System;
using System.Collections.Generic;

namespace Quotations.Api.Models.Dtos;

/// <summary>
/// Data transfer object for quotation API responses
/// </summary>
public class QuotationDto
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public AuthorDto Author { get; set; } = new();
    public SourceDto Source { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<string> PotentialDuplicateIds { get; set; } = new();
    public AiReviewDto? AiReview { get; set; }
}

public class AiReviewDto
{
    public string Status { get; set; } = string.Empty;
    public string? ModelUsed { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Authenticity / analysis fields (full detail — GET /quotations/{id})
    public string? Summary { get; set; }
    public bool? IsLikelyAuthentic { get; set; }
    public string? AuthenticityReasoning { get; set; }
    public string? CorrectAttribution { get; set; }
    public string? ApproximateEra { get; set; }
    public string? Language { get; set; }
    public int? QualityScore { get; set; }
    public string? Mood { get; set; }
}

/// <summary>
/// Author information in DTO
/// </summary>
public class AuthorDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Lifespan { get; set; }
    public string? Occupation { get; set; }
}

/// <summary>
/// Source information in DTO
/// </summary>
public class SourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? Year { get; set; }
}

public class AiReviewSummaryDto
{
    public string Status { get; set; } = string.Empty;
    public string? ModelUsed { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Slim authenticity fields — enough to render the Verified/Disputed badge, mood chip,
    // and the misattributed collection (which needs the corrected attribution + reasoning).
    public string? Summary { get; set; }
    public bool? IsLikelyAuthentic { get; set; }
    public string? AuthenticityReasoning { get; set; }
    public string? CorrectAttribution { get; set; }
    public string? ApproximateEra { get; set; }
    public string? Language { get; set; }
    public int? QualityScore { get; set; }
    public string? Mood { get; set; }
}

/// <summary>
/// Slim quotation returned in list/search/random-batch endpoints.
/// Omits heavy AI reasoning fields to keep page responses small.
/// </summary>
public class QuotationSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public AuthorDto Author { get; set; } = new();
    public SourceDto Source { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<string> PotentialDuplicateIds { get; set; } = new();
    public AiReviewSummaryDto? AiReview { get; set; }
}

/// <summary>
/// Paginated response for quotations list (uses slim summary DTO)
/// </summary>
public class PaginatedQuotationsResponse
{
    public List<QuotationSummaryDto> Items { get; set; } = new();
    public PaginationMetadata Pagination { get; set; } = new();
}
