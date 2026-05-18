using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quotations.Api.Services;

public class AiReviewService
{
    private readonly IAnthropicService _anthropic;
    private readonly IQuotationRepository _quotationRepository;
    private readonly IAiReviewErrorRepository _errorRepository;
    private readonly AiReviewOptions _options;
    private readonly ILogger<AiReviewService> _logger;

    public AiReviewService(
        IAnthropicService anthropic,
        IQuotationRepository quotationRepository,
        IAiReviewErrorRepository errorRepository,
        IOptions<AiReviewOptions> options,
        ILogger<AiReviewService> logger)
    {
        _anthropic = anthropic;
        _quotationRepository = quotationRepository;
        _errorRepository = errorRepository;
        _options = options.Value;
        _logger = logger;
    }

    // Fields scoring below this threshold trigger a targeted fix pass.
    private const int FixThreshold = 8;

    public async Task ReviewQuotationAsync(Quotation quotation)
    {
        _logger.LogInformation("Starting AI review for quotation {QuotationId}", quotation.Id);

        quotation.AiReview.Status = AiReviewStatus.InProgress;
        quotation.AiReview.LastAttemptAt = DateTime.UtcNow;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        try
        {
            // Pass 1: score all three dimensions and assign tags
            var triage = await _anthropic.TriageQuotationAsync(
                quotation.Text, quotation.Author.Name,
                quotation.Source.Title, quotation.Source.Type.ToString());

            if (triage == null)
            {
                quotation.AiReview.Status = AiReviewStatus.NotReviewed;
                quotation.AiReview.FailureReason = "AI analysis not available (API key not configured)";
                await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                return;
            }

            // Pass 2: targeted fix for each low-scoring field (run in parallel)
            var fixTasks = new
            {
                Quote       = triage.QuoteScore       < FixThreshold ? _anthropic.FixFieldAsync("quote",  quotation.Text, quotation.Author.Name, quotation.Source.Title, quotation.Source.Type.ToString(), _options.UseWebSearch) : Task.FromResult<AiFixResult?>(null),
                Attribution = triage.AttributionScore < FixThreshold ? _anthropic.FixFieldAsync("author", quotation.Text, quotation.Author.Name, quotation.Source.Title, quotation.Source.Type.ToString(), _options.UseWebSearch) : Task.FromResult<AiFixResult?>(null),
                Source      = triage.SourceScore      < FixThreshold ? _anthropic.FixFieldAsync("source", quotation.Text, quotation.Author.Name, quotation.Source.Title, quotation.Source.Type.ToString(), _options.UseWebSearch) : Task.FromResult<AiFixResult?>(null),
            };
            await Task.WhenAll(fixTasks.Quote, fixTasks.Attribution, fixTasks.Source);

            var quoteFix       = await fixTasks.Quote;
            var attributionFix = await fixTasks.Attribution;
            var sourceFix      = await fixTasks.Source;

            var modelUsed = quoteFix?.ModelUsed ?? attributionFix?.ModelUsed ?? sourceFix?.ModelUsed ?? triage.ModelUsed;

            quotation.AiReview.Status    = AiReviewStatus.Reviewed;
            quotation.AiReview.ModelUsed = modelUsed;
            quotation.AiReview.ReviewedAt = DateTime.UtcNow;
            quotation.AiReview.ProcessingSnapshot = new AiProcessingSnapshot
            {
                Model = modelUsed,
                MaxTokens = _options.MaxTokens,
                WebSearchEnabled = _options.UseWebSearch,
                ConcurrentRequests = _options.ConcurrentRequests,
                BatchSize = _options.BatchSize
            };

            quotation.AiReview.QuoteAccuracy       = ToScoreWithSuggestion(triage.QuoteScore, quoteFix);
            quotation.AiReview.AttributionAccuracy = ToScoreWithSuggestion(triage.AttributionScore, attributionFix);
            quotation.AiReview.SourceAccuracy      = ToScoreWithSuggestion(triage.SourceScore, sourceFix);
            quotation.AiReview.SuggestedTags       = triage.Tags.Select(t => t.Tag).ToList();

            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

            // Build the AiAnalysisResult that ApplyAiFixesAsync expects
            var analysisResult = new AiAnalysisResult(
                ToScoreResult(triage.QuoteScore, quoteFix),
                ToScoreResult(triage.AttributionScore, attributionFix),
                ToScoreResult(triage.SourceScore, sourceFix),
                string.Empty,
                triage.Tags,
                modelUsed,
                null,
                null,
                null,
                new List<string>());

            await ApplyAiFixesAsync(quotation, analysisResult);

            _logger.LogInformation("Completed AI review for quotation {QuotationId} using {Model}", quotation.Id, modelUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI review failed for quotation {QuotationId} (attempt {Attempt})", quotation.Id, quotation.AiReview.RetryCount + 1);

            quotation.AiReview.RetryCount++;
            quotation.AiReview.FailureReason = ex.Message;

            if (quotation.AiReview.RetryCount >= _options.MaxRetries)
            {
                quotation.AiReview.Status = AiReviewStatus.Failed;
                await _errorRepository.CreateAsync(new AiReviewError
                {
                    QuotationId = quotation.Id,
                    QuotationText = quotation.Text,
                    AuthorName = quotation.Author.Name,
                    LastError = ex.Message,
                    RetryCount = quotation.AiReview.RetryCount,
                    FailedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogWarning("Quotation {QuotationId} moved to error queue after {Retries} failed attempts", quotation.Id, quotation.AiReview.RetryCount);
            }
            else
            {
                quotation.AiReview.Status = AiReviewStatus.Pending;
            }

            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
        }
    }

    private static AiScoreWithSuggestion ToScoreWithSuggestion(int score, AiFixResult? fix) =>
        new()
        {
            Score = score,
            Reasoning = fix?.Reasoning ?? string.Empty,
            SuggestedValue = fix?.SuggestedValue,
            SuggestionConfidence = fix?.Confidence,
            WasAiFilled = fix?.WasAiFilled ?? false,
            Citations = fix?.Citations ?? new List<string>()
        };

    private static AiScoreResult ToScoreResult(int score, AiFixResult? fix) =>
        new(score,
            fix?.Reasoning ?? string.Empty,
            fix?.SuggestedValue,
            fix?.Confidence,
            fix?.WasAiFilled ?? false,
            fix?.Citations ?? new List<string>());

    public async Task ApplyTriageBatchResultAsync(Quotation quotation, string contentText, string modelUsed)
    {
        quotation.AiReview ??= new AiReview();

        var triage = _anthropic.ParseTriageBatchResult(contentText, modelUsed);
        if (triage == null)
        {
            quotation.AiReview.Status = AiReviewStatus.Failed;
            quotation.AiReview.FailureReason = "Failed to parse triage batch result";
            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
            return;
        }

        quotation.AiReview.ModelUsed = modelUsed;
        quotation.AiReview.ReviewedAt = DateTime.UtcNow;
        quotation.AiReview.QuoteAccuracy       = new AiScoreWithSuggestion { Score = triage.QuoteScore };
        quotation.AiReview.AttributionAccuracy = new AiScoreWithSuggestion { Score = triage.AttributionScore };
        quotation.AiReview.SourceAccuracy      = new AiScoreWithSuggestion { Score = triage.SourceScore };
        quotation.AiReview.SuggestedTags       = triage.Tags.Select(t => t.Tag).ToList();

        var needsFix = triage.QuoteScore < FixThreshold
                    || triage.AttributionScore < FixThreshold
                    || triage.SourceScore < FixThreshold;

        quotation.AiReview.Status = needsFix ? AiReviewStatus.FixPending : AiReviewStatus.Reviewed;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        // Apply tags immediately even if no fix is needed
        var tagsOnly = new AiAnalysisResult(
            new AiScoreResult(triage.QuoteScore, string.Empty, null, null, false, new List<string>()),
            new AiScoreResult(triage.AttributionScore, string.Empty, null, null, false, new List<string>()),
            new AiScoreResult(triage.SourceScore, string.Empty, null, null, false, new List<string>()),
            string.Empty, triage.Tags, modelUsed, null, null, null, new List<string>());

        await ApplyAiFixesAsync(quotation, tagsOnly);
    }

    public async Task ApplyFixBatchResultsAsync(
        Quotation quotation,
        IEnumerable<(string Field, string ContentText)> fieldResults,
        string modelUsed)
    {
        quotation.AiReview ??= new AiReview();

        AiFixResult? quoteFix = null, authorFix = null, sourceFix = null;
        foreach (var (field, contentText) in fieldResults)
        {
            var fix = _anthropic.ParseFixBatchResult(contentText, modelUsed);
            switch (field)
            {
                case "quote":  quoteFix  = fix; break;
                case "author": authorFix = fix; break;
                case "source": sourceFix = fix; break;
            }
        }

        if (quoteFix  != null) quotation.AiReview.QuoteAccuracy       = ToScoreWithSuggestion(quotation.AiReview.QuoteAccuracy?.Score       ?? 0, quoteFix);
        if (authorFix != null) quotation.AiReview.AttributionAccuracy = ToScoreWithSuggestion(quotation.AiReview.AttributionAccuracy?.Score ?? 0, authorFix);
        if (sourceFix != null) quotation.AiReview.SourceAccuracy      = ToScoreWithSuggestion(quotation.AiReview.SourceAccuracy?.Score      ?? 0, sourceFix);

        quotation.AiReview.Status = AiReviewStatus.Reviewed;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        var analysisResult = new AiAnalysisResult(
            ToScoreResult(quotation.AiReview.QuoteAccuracy?.Score       ?? 0, quoteFix),
            ToScoreResult(quotation.AiReview.AttributionAccuracy?.Score ?? 0, authorFix),
            ToScoreResult(quotation.AiReview.SourceAccuracy?.Score      ?? 0, sourceFix),
            string.Empty,
            quotation.AiReview.SuggestedTags.Select(t => new AiTagSuggestion(t, 100)).ToList(),
            modelUsed,
            null, null, null, new List<string>());

        await ApplyAiFixesAsync(quotation, analysisResult);
    }

    public async Task ReviewFromBatchResultAsync(Quotation quotation, string contentText, string modelUsed)
    {
        quotation.AiReview ??= new AiReview();

        var result = _anthropic.ParseBatchResultContent(contentText, modelUsed);
        if (result == null)
        {
            quotation.AiReview.Status = AiReviewStatus.Failed;
            quotation.AiReview.FailureReason = "Failed to parse batch result content";
            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
            return;
        }

        quotation.AiReview.Status = AiReviewStatus.Reviewed;
        quotation.AiReview.ModelUsed = result.ModelUsed;
        quotation.AiReview.ReviewedAt = DateTime.UtcNow;
        quotation.AiReview.ProcessingSnapshot = new AiProcessingSnapshot
        {
            Model = result.ModelUsed,
            MaxTokens = _options.MaxTokens,
            WebSearchEnabled = false,
            ConcurrentRequests = 0,
            BatchSize = 0
        };
        quotation.AiReview.Summary = result.Summary;

        quotation.AiReview.QuoteAccuracy = new AiScoreWithSuggestion
        {
            Score = result.QuoteAccuracy.Score,
            Reasoning = result.QuoteAccuracy.Reasoning,
            SuggestedValue = result.QuoteAccuracy.SuggestedValue,
            SuggestionConfidence = result.QuoteAccuracy.SuggestionConfidence,
            WasAiFilled = result.QuoteAccuracy.WasAiFilled,
            Citations = result.QuoteAccuracy.Citations
        };
        quotation.AiReview.AttributionAccuracy = new AiScoreWithSuggestion
        {
            Score = result.AttributionAccuracy.Score,
            Reasoning = result.AttributionAccuracy.Reasoning,
            SuggestedValue = result.AttributionAccuracy.SuggestedValue,
            SuggestionConfidence = result.AttributionAccuracy.SuggestionConfidence,
            WasAiFilled = result.AttributionAccuracy.WasAiFilled,
            Citations = result.AttributionAccuracy.Citations
        };
        quotation.AiReview.SourceAccuracy = new AiScoreWithSuggestion
        {
            Score = result.SourceAccuracy.Score,
            Reasoning = result.SourceAccuracy.Reasoning,
            SuggestedValue = result.SourceAccuracy.SuggestedValue,
            SuggestionConfidence = result.SourceAccuracy.SuggestionConfidence,
            WasAiFilled = result.SourceAccuracy.WasAiFilled,
            Citations = result.SourceAccuracy.Citations
        };

        quotation.AiReview.SuggestedTags = result.TagSuggestions.Select(t => t.Tag).ToList();
        quotation.AiReview.IsLikelyAuthentic = result.IsLikelyAuthentic;
        quotation.AiReview.AuthenticityReasoning = result.AuthenticityReasoning;
        quotation.AiReview.ApproximateEra = result.ApproximateEra;
        quotation.AiReview.KnownVariants = result.KnownVariants;

        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
        await ApplyAiFixesAsync(quotation, result);
    }

    private async Task ApplyAiFixesAsync(Quotation quotation, AiAnalysisResult result)
    {
        const int ConfidenceThreshold = 70;
        const int FillConfidenceThreshold = 50;
        const int TagConfidenceThreshold = 50;
        var changes = new List<AiFieldChange>();

        if (ShouldApply(result.QuoteAccuracy, ConfidenceThreshold, FillConfidenceThreshold))
        {
            var newText = TextNormalizer.Normalize(result.QuoteAccuracy.SuggestedValue!);
            changes.Add(new AiFieldChange
            {
                Field = "Text",
                PreviousValue = quotation.Text,
                NewValue = newText,
                Reasoning = result.QuoteAccuracy.Reasoning,
                Confidence = result.QuoteAccuracy.SuggestionConfidence!.Value
            });
            quotation.Text = newText;
        }
        else
        {
            // Always normalize spacing on the existing text even when no AI correction is applied
            quotation.Text = TextNormalizer.Normalize(quotation.Text);
        }

        var authorOverwrite = IsPlaceholder(quotation.Author.Name) ? FillConfidenceThreshold : ConfidenceThreshold;
        if (ShouldApply(result.AttributionAccuracy, authorOverwrite, FillConfidenceThreshold))
        {
            var newAuthor = TextNormalizer.Normalize(result.AttributionAccuracy.SuggestedValue!);
            changes.Add(new AiFieldChange
            {
                Field = "Author",
                PreviousValue = quotation.Author.Name,
                NewValue = newAuthor,
                Reasoning = result.AttributionAccuracy.Reasoning,
                Confidence = result.AttributionAccuracy.SuggestionConfidence!.Value
            });
            quotation.Author.Name = newAuthor;
        }

        var sourceOverwrite = IsPlaceholder(quotation.Source.Title) ? FillConfidenceThreshold : ConfidenceThreshold;
        if (ShouldApply(result.SourceAccuracy, sourceOverwrite, FillConfidenceThreshold))
        {
            var newSource = TextNormalizer.Normalize(result.SourceAccuracy.SuggestedValue!);
            changes.Add(new AiFieldChange
            {
                Field = "Source",
                PreviousValue = quotation.Source.Title,
                NewValue = newSource,
                Reasoning = result.SourceAccuracy.Reasoning,
                Confidence = result.SourceAccuracy.SuggestionConfidence!.Value
            });
            quotation.Source.Title = newSource;
        }

        var highConfidenceTags = result.TagSuggestions
            .Where(t => t.Confidence >= TagConfidenceThreshold)
            .Select(t => t.Tag)
            .ToList();

        if (highConfidenceTags.Count > 0)
        {
            var previousTags = quotation.Tags.ToList();
            var merged = previousTags
                .Union(highConfidenceTags, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (merged.Count != previousTags.Count)
            {
                var added = merged.Except(previousTags, StringComparer.OrdinalIgnoreCase).ToList();
                changes.Add(new AiFieldChange
                {
                    Field = "Tags",
                    PreviousValue = string.Join(", ", previousTags),
                    NewValue = string.Join(", ", merged),
                    Reasoning = $"Added: {string.Join(", ", added)}",
                    Confidence = highConfidenceTags
                        .Select(t => result.TagSuggestions.First(s => s.Tag.Equals(t, StringComparison.OrdinalIgnoreCase)).Confidence)
                        .Min()
                });
                quotation.Tags = merged;
            }
        }

        if (changes.Count == 0)
            return;

        quotation.AiRevisions.Add(new AiRevision
        {
            AppliedAt = DateTime.UtcNow,
            ModelUsed = result.ModelUsed,
            Changes = changes
        });

        _logger.LogInformation("AI fix: applied {Count} change(s) to {QuotationId}", changes.Count, quotation.Id);
        await _quotationRepository.UpdateQuotationAsync(quotation);
    }

    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "unknown", "other", "n/a", "none", "various", "anonymous", "unattributed"
    };

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) || PlaceholderValues.Contains(value.Trim());

    private static bool ShouldApply(AiScoreResult score, int overwriteThreshold, int fillThreshold)
    {
        if (string.IsNullOrWhiteSpace(score.SuggestedValue) || score.SuggestionConfidence == null)
            return false;

        // Filling a blank field: accept at the lower threshold
        if (score.WasAiFilled)
            return score.SuggestionConfidence >= fillThreshold;

        // Overwriting an existing value: require the higher threshold.
        // A low accuracy score means the current value is wrong — confidence is what
        // controls whether the suggested correction is trustworthy enough to apply.
        return score.SuggestionConfidence >= overwriteThreshold;
    }
}
