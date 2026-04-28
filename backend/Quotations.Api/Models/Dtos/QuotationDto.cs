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
    public AiReviewDto? AiReview { get; set; }
}

public class AiReviewDto
{
    public string Status { get; set; } = string.Empty;
    public string? ModelUsed { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public AiScoreDto? QuoteAccuracy { get; set; }
    public AiScoreDto? AttributionAccuracy { get; set; }
    public AiScoreDto? SourceAccuracy { get; set; }
    public string? Summary { get; set; }
    public List<string> SuggestedTags { get; set; } = new();
}

public class AiScoreDto
{
    public int Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string? SuggestedValue { get; set; }
    public bool WasAiFilled { get; set; }
    public List<string> Citations { get; set; } = new();
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

/// <summary>
/// Paginated response for quotations list
/// </summary>
public class PaginatedQuotationsResponse
{
    public List<QuotationDto> Items { get; set; } = new();
    public PaginationMetadata Pagination { get; set; } = new();
}
