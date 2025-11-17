using System.Collections.Generic;

namespace Quotations.Api.Models.Dtos;

/// <summary>
/// Request DTO for submitting a new quotation
/// </summary>
public class SubmitQuotationRequest
{
    /// <summary>
    /// The quotation text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Author name
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Author lifespan (optional, e.g., "1809-1865")
    /// </summary>
    public string? AuthorLifespan { get; set; }

    /// <summary>
    /// Author occupation (optional)
    /// </summary>
    public string? AuthorOccupation { get; set; }

    /// <summary>
    /// Source title
    /// </summary>
    public string SourceTitle { get; set; } = string.Empty;

    /// <summary>
    /// Source type (book, movie, speech, interview, other)
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Source year (optional)
    /// </summary>
    public int? SourceYear { get; set; }

    /// <summary>
    /// Source additional info (optional)
    /// </summary>
    public string? SourceAdditionalInfo { get; set; }

    /// <summary>
    /// Tags for the quotation
    /// </summary>
    public List<string> Tags { get; set; } = new();
}