using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Quotations.Api.Models;

public enum AiBatchJobStatus
{
    Submitted,
    InProgress,
    Completed,
    Failed
}

public enum AiBatchJobPhase
{
    Triage,  // legacy — kept for existing documents
    Fix,     // legacy — kept for existing documents
    Review   // lean single-pass
}

public class AiBatchJob
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string AnthropicBatchId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public AiBatchJobStatus Status { get; set; } = AiBatchJobStatus.Submitted;

    [BsonRepresentation(BsonType.String)]
    public AiBatchJobPhase Phase { get; set; } = AiBatchJobPhase.Triage;

    public List<string> QuotationIds { get; set; } = new();

    public int TotalCount { get; set; }

    public int SucceededCount { get; set; }

    public int FailedCount { get; set; }

    public string ModelUsed { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
