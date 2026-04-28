using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System;
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
        await _quotationRepository.UpdateQuotationAsync(quotation);

        try
        {
            var result = await _anthropic.AnalyzeQuotationAsync(
                quotation.Text,
                quotation.Author.Name,
                null,
                quotation.Source.Title,
                quotation.Source.Type.ToString(),
                null);

            if (result == null)
            {
                // API key not configured — mark as not reviewed so it's skipped gracefully
                quotation.AiReview.Status = AiReviewStatus.NotReviewed;
                quotation.AiReview.FailureReason = "AI analysis not available (API key not configured)";
                await _quotationRepository.UpdateQuotationAsync(quotation);
                return;
            }

            quotation.AiReview.Status = AiReviewStatus.Reviewed;
            quotation.AiReview.ModelUsed = result.ModelUsed;
            quotation.AiReview.ReviewedAt = DateTime.UtcNow;
            quotation.AiReview.Summary = result.Summary;

            quotation.AiReview.QuoteAccuracy = new AiScoreWithSuggestion
            {
                Score = result.QuoteAccuracy.Score,
                Reasoning = result.QuoteAccuracy.Reasoning,
                SuggestedValue = result.QuoteAccuracy.SuggestedValue,
                WasAiFilled = result.QuoteAccuracy.WasAiFilled,
                Citations = result.QuoteAccuracy.Citations
            };

            quotation.AiReview.AttributionAccuracy = new AiScoreWithSuggestion
            {
                Score = result.AttributionAccuracy.Score,
                Reasoning = result.AttributionAccuracy.Reasoning,
                SuggestedValue = result.AttributionAccuracy.SuggestedValue,
                WasAiFilled = result.AttributionAccuracy.WasAiFilled,
                Citations = result.AttributionAccuracy.Citations
            };

            quotation.AiReview.SourceAccuracy = new AiScoreWithSuggestion
            {
                Score = result.SourceAccuracy.Score,
                Reasoning = result.SourceAccuracy.Reasoning,
                SuggestedValue = result.SourceAccuracy.SuggestedValue,
                WasAiFilled = result.SourceAccuracy.WasAiFilled,
                Citations = result.SourceAccuracy.Citations
            };

            quotation.AiReview.SuggestedTags = result.SuggestedTags;

            await _quotationRepository.UpdateQuotationAsync(quotation);
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

            await _quotationRepository.UpdateQuotationAsync(quotation);
        }
    }
}
