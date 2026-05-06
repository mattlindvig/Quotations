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

    public async Task ReviewQuotationAsync(Quotation quotation)
    {
        _logger.LogInformation("Starting AI review for quotation {QuotationId}", quotation.Id);

        quotation.AiReview.Status = AiReviewStatus.InProgress;
        quotation.AiReview.LastAttemptAt = DateTime.UtcNow;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        try
        {
            var result = await _anthropic.AnalyzeQuotationAsync(
                quotation.Text,
                quotation.Author.Name,
                null,
                quotation.Source.Title,
                quotation.Source.Type.ToString(),
                null,
                _options.UseWebSearch);

            if (result == null)
            {
                // API key not configured — mark as not reviewed so it's skipped gracefully
                quotation.AiReview.Status = AiReviewStatus.NotReviewed;
                quotation.AiReview.FailureReason = "AI analysis not available (API key not configured)";
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
                WebSearchEnabled = _options.UseWebSearch,
                ConcurrentRequests = _options.ConcurrentRequests,
                BatchSize = _options.BatchSize
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

            _logger.LogInformation("Completed AI review for quotation {QuotationId} using {Model}", quotation.Id, result.ModelUsed);
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

    private async Task ApplyAiFixesAsync(Quotation quotation, AiAnalysisResult result)
    {
        const int ConfidenceThreshold = 80;
        const int FillConfidenceThreshold = 50; // lower bar when filling a blank field
        const int TagConfidenceThreshold = 50;
        var changes = new List<AiFieldChange>();

        if (ShouldApply(result.QuoteAccuracy, ConfidenceThreshold, FillConfidenceThreshold))
        {
            changes.Add(new AiFieldChange
            {
                Field = "Text",
                PreviousValue = quotation.Text,
                NewValue = result.QuoteAccuracy.SuggestedValue!,
                Reasoning = result.QuoteAccuracy.Reasoning,
                Confidence = result.QuoteAccuracy.SuggestionConfidence!.Value
            });
            quotation.Text = result.QuoteAccuracy.SuggestedValue!;
        }

        if (ShouldApply(result.AttributionAccuracy, ConfidenceThreshold, FillConfidenceThreshold))
        {
            changes.Add(new AiFieldChange
            {
                Field = "Author",
                PreviousValue = quotation.Author.Name,
                NewValue = result.AttributionAccuracy.SuggestedValue!,
                Reasoning = result.AttributionAccuracy.Reasoning,
                Confidence = result.AttributionAccuracy.SuggestionConfidence!.Value
            });
            quotation.Author.Name = result.AttributionAccuracy.SuggestedValue!;
        }

        if (ShouldApply(result.SourceAccuracy, ConfidenceThreshold, FillConfidenceThreshold))
        {
            changes.Add(new AiFieldChange
            {
                Field = "Source",
                PreviousValue = quotation.Source.Title,
                NewValue = result.SourceAccuracy.SuggestedValue!,
                Reasoning = result.SourceAccuracy.Reasoning,
                Confidence = result.SourceAccuracy.SuggestionConfidence!.Value
            });
            quotation.Source.Title = result.SourceAccuracy.SuggestedValue!;
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

    private static bool ShouldApply(AiScoreResult score, int overwriteThreshold, int fillThreshold)
    {
        if (string.IsNullOrWhiteSpace(score.SuggestedValue) || score.SuggestionConfidence == null)
            return false;

        // Low scores mean the AI couldn't verify the content — the suggestedValue is a
        // description of the problem, not a real corrected value. Never auto-apply.
        if (score.Score < 5)
            return false;

        // Filling a blank field: accept at the lower threshold
        if (score.WasAiFilled)
            return score.SuggestionConfidence >= fillThreshold;

        // Overwriting an existing value: require the higher threshold
        return score.SuggestionConfidence >= overwriteThreshold;
    }
}
