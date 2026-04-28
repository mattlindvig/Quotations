using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Quotations.Api.Models;

public enum AiReviewStatus
{
    NotReviewed,
    Pending,
    InProgress,
    Reviewed,
    Failed
}

public class AiScoreWithSuggestion
{
    public int Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string? SuggestedValue { get; set; }
    public bool WasAiFilled { get; set; }
    public List<string> Citations { get; set; } = new();
}

public class AiReview
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AiReviewStatus Status { get; set; } = AiReviewStatus.NotReviewed;

    public string? ModelUsed { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime? LastAttemptAt { get; set; }

    public string? FailureReason { get; set; }

    public AiScoreWithSuggestion? QuoteAccuracy { get; set; }

    public AiScoreWithSuggestion? AttributionAccuracy { get; set; }

    public AiScoreWithSuggestion? SourceAccuracy { get; set; }

    public string? Summary { get; set; }

    public List<string> SuggestedTags { get; set; } = new();
}
