using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Quotations.Api.Models;

/// <summary>
/// Quotation status enumeration
/// </summary>
public enum QuotationStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Embedded reference to an author
/// </summary>
public class AuthorReference
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Embedded reference to a source
/// </summary>
public class SourceReference
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public SourceType Type { get; set; }
}

/// <summary>
/// Embedded reference to a user
/// </summary>
public class UserReference
{
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Represents a quotation with all metadata, review status, and audit trail
/// MongoDB Collection: quotations
/// </summary>
public class Quotation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The quotation text
    /// </summary>
    [BsonElement("text")]
    [BsonRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Reference to author (denormalized for read performance)
    /// </summary>
    [BsonElement("author")]
    [BsonRequired]
    public AuthorReference Author { get; set; } = new();

    /// <summary>
    /// Reference to source (denormalized for read performance)
    /// </summary>
    [BsonElement("source")]
    [BsonRequired]
    public SourceReference Source { get; set; } = new();

    /// <summary>
    /// Array of tag names
    /// </summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Review status (pending, approved, rejected)
    /// </summary>
    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public QuotationStatus Status { get; set; } = QuotationStatus.Pending;

    /// <summary>
    /// User who submitted this quotation (null for seeded data)
    /// </summary>
    [BsonElement("submittedBy")]
    public UserReference? SubmittedBy { get; set; }

    /// <summary>
    /// Submission timestamp
    /// </summary>
    [BsonElement("submittedAt")]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reviewer who processed this quotation
    /// </summary>
    [BsonElement("reviewedBy")]
    public UserReference? ReviewedBy { get; set; }

    /// <summary>
    /// Review timestamp
    /// </summary>
    [BsonElement("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Reason for rejection (required if status is rejected)
    /// </summary>
    [BsonElement("rejectionReason")]
    public string? RejectionReason { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
