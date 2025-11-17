using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Quotations.Api.Models;

/// <summary>
/// Represents an author who said or wrote a quotation.
/// MongoDB Collection: authors
/// </summary>
public class Author
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the author (primary display)
    /// </summary>
    [BsonElement("name")]
    [BsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Lifespan in format "YYYY-YYYY" or "YYYY-present" (optional)
    /// </summary>
    [BsonElement("lifespan")]
    public string? Lifespan { get; set; }

    /// <summary>
    /// Occupation description (e.g., "Philosopher, Writer")
    /// </summary>
    [BsonElement("occupation")]
    public string? Occupation { get; set; }

    /// <summary>
    /// Short biography (optional)
    /// </summary>
    [BsonElement("biography")]
    public string? Biography { get; set; }

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
