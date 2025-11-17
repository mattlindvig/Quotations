using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Quotations.Api.Models;

/// <summary>
/// Source type enumeration
/// </summary>
public enum SourceType
{
    Book,
    Movie,
    Speech,
    Interview,
    Other
}

/// <summary>
/// Represents the origin of a quotation (book, movie, speech, etc.)
/// MongoDB Collection: sources
/// </summary>
public class Source
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Source title
    /// </summary>
    [BsonElement("title")]
    [BsonRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Source type (book, movie, speech, interview, other)
    /// </summary>
    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    [BsonRequired]
    public SourceType Type { get; set; }

    /// <summary>
    /// Publication/release year (optional)
    /// </summary>
    [BsonElement("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Additional information like publisher, director, etc. (optional)
    /// </summary>
    [BsonElement("additionalInfo")]
    public string? AdditionalInfo { get; set; }

    /// <summary>
    /// Denormalized count of approved quotations for performance
    /// </summary>
    [BsonElement("quotationCount")]
    public int QuotationCount { get; set; } = 0;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
