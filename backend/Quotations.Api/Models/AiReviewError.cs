using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Quotations.Api.Models;

public class AiReviewError
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string QuotationId { get; set; } = string.Empty;

    public string QuotationText { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public string LastError { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public DateTime FailedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
