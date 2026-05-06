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
    Failed
}

public class AiScoreWithSuggestion
{
    public int Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string? SuggestedValue { get; set; }
    public int? SuggestionConfidence { get; set; }
    public bool WasAiFilled { get; set; }
    public List<string> Citations { get; set; } = new();
}

public class AiFieldChange
{
    public string Field { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public int Confidence { get; set; }
}

public class AiRevision
{
    public DateTime AppliedAt { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public List<AiFieldChange> Changes { get; set; } = new();
}

public class AiProcessingSnapshot
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public bool WebSearchEnabled { get; set; }
    public int ConcurrentRequests { get; set; }
    public int BatchSize { get; set; }
}

public class AiReview
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AiReviewStatus Status { get; set; } = AiReviewStatus.NotReviewed;

    public string? ModelUsed { get; set; }

    public AiProcessingSnapshot? ProcessingSnapshot { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime? LastAttemptAt { get; set; }

    public string? FailureReason { get; set; }

    public AiScoreWithSuggestion? QuoteAccuracy { get; set; }

    public AiScoreWithSuggestion? AttributionAccuracy { get; set; }

    public AiScoreWithSuggestion? SourceAccuracy { get; set; }

    public string? Summary { get; set; }

    public List<string> SuggestedTags { get; set; } = new();

    public bool? IsLikelyAuthentic { get; set; }

    public string? AuthenticityReasoning { get; set; }

    public string? ApproximateEra { get; set; }

    public List<string> KnownVariants { get; set; } = new();
}
