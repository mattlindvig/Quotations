using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Quotations.Api.Models;

public enum AiReviewStatus
{
    NotReviewed,
    Pending,
    InProgress,
    Reviewed,
    Failed,
    BatchPending,
    Rejected,      // pre-filtered as garbage, or AI flagged reject=true
    FixPending     // legacy — no longer produced, kept for existing documents
}

/// <summary>
/// A single field value changed by AI review.
/// </summary>
[BsonIgnoreExtraElements]
public class AiFieldChange
{
    public string Field { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}

/// <summary>
/// One AI review pass that produced one or more field corrections.
/// </summary>
[BsonIgnoreExtraElements]
public class AiRevision
{
    public DateTime AppliedAt { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public List<AiFieldChange> Changes { get; set; } = new();
}

/// <summary>
/// Minimal AI review metadata stored per quotation.
/// All score/suggestion/authenticity fields from earlier versions are intentionally
/// omitted — they ballooned document size without being surfaced to users.
/// BsonIgnoreExtraElements ensures old documents with those fields still deserialize.
/// </summary>
[BsonIgnoreExtraElements]
public class AiReview
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AiReviewStatus Status { get; set; } = AiReviewStatus.NotReviewed;

    public string? ModelUsed { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime? LastAttemptAt { get; set; }

    public string? FailureReason { get; set; }
}
