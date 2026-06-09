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
        _logger.LogInformation("Starting AI review for {QuotationId}", quotation.Id);

        quotation.AiReview.Status = AiReviewStatus.InProgress;
        quotation.AiReview.LastAttemptAt = DateTime.UtcNow;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        try
        {
            var result = await _anthropic.LeanReviewAsync(
                quotation.Text, quotation.Author.Name,
                quotation.Source.Title, quotation.Source.Type.ToString());

            if (result == null)
            {
                quotation.AiReview.Status = AiReviewStatus.NotReviewed;
                quotation.AiReview.FailureReason = "AI analysis not available (API key not configured)";
                await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                return;
            }

            quotation.AiReview.ModelUsed = result.ModelUsed;
            quotation.AiReview.ReviewedAt = DateTime.UtcNow;

            if (result.Reject)
            {
                quotation.AiReview.Status = AiReviewStatus.Rejected;
                quotation.AiReview.FailureReason = "AI flagged as non-quotation";
                await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                _logger.LogInformation("AI rejected {QuotationId} as non-quotation", quotation.Id);
                return;
            }

            quotation.AiReview.Status = AiReviewStatus.Reviewed;
            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

            await ApplyLeanResultAsync(quotation, result);

            _logger.LogInformation("Completed AI review for {QuotationId} using {Model}", quotation.Id, result.ModelUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI review failed for {QuotationId} (attempt {Attempt})",
                quotation.Id, quotation.AiReview.RetryCount + 1);

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
                _logger.LogWarning("Quotation {QuotationId} moved to error queue after {Retries} failed attempts",
                    quotation.Id, quotation.AiReview.RetryCount);
            }
            else
            {
                quotation.AiReview.Status = AiReviewStatus.Pending;
            }

            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
        }
    }

    /// <summary>
    /// Apply a lean review result from either real-time or batch processing.
    /// </summary>
    public async Task ApplyLeanResultAsync(Quotation quotation, LeanReviewResult result)
    {
        var changes = new List<AiFieldChange>();

        // Text — normalize spacing regardless; apply correction only if provided
        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            var newText = TextNormalizer.Normalize(result.Text);
            if (!string.Equals(newText, quotation.Text, StringComparison.Ordinal))
            {
                changes.Add(new AiFieldChange { Field = "Text", PreviousValue = quotation.Text, NewValue = newText });
                quotation.Text = newText;
            }
        }
        else
        {
            quotation.Text = TextNormalizer.Normalize(quotation.Text);
        }

        // Author
        if (!string.IsNullOrWhiteSpace(result.Author))
        {
            var newAuthor = TextNormalizer.Normalize(result.Author);
            if (!string.Equals(newAuthor, quotation.Author.Name, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new AiFieldChange { Field = "Author", PreviousValue = quotation.Author.Name, NewValue = newAuthor });
                quotation.Author.Name = newAuthor;
            }
        }

        // Source
        if (!string.IsNullOrWhiteSpace(result.Source))
        {
            var newSource = TextNormalizer.Normalize(result.Source);
            if (!string.Equals(newSource, quotation.Source.Title, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new AiFieldChange { Field = "Source", PreviousValue = quotation.Source.Title, NewValue = newSource });
                quotation.Source.Title = newSource;
            }
        }

        // Tags — merge canonical suggestions into existing tags
        if (result.Tags.Count > 0)
        {
            var previousTags = quotation.Tags.ToList();
            var merged = previousTags
                .Union(result.Tags, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (merged.Count != previousTags.Count)
            {
                changes.Add(new AiFieldChange
                {
                    Field = "Tags",
                    PreviousValue = string.Join(", ", previousTags),
                    NewValue = string.Join(", ", merged)
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

    /// <summary>
    /// Process a lean batch result for a single quotation and persist changes.
    /// </summary>
    public async Task ApplyLeanBatchResultAsync(Quotation quotation, string contentText, string modelUsed)
    {
        quotation.AiReview ??= new AiReview();

        var result = _anthropic.ParseLeanBatchResult(contentText, modelUsed);
        if (result == null)
        {
            quotation.AiReview.Status = AiReviewStatus.Failed;
            quotation.AiReview.FailureReason = "Failed to parse lean batch result";
            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
            return;
        }

        quotation.AiReview.ModelUsed = modelUsed;
        quotation.AiReview.ReviewedAt = DateTime.UtcNow;

        if (result.Reject)
        {
            quotation.AiReview.Status = AiReviewStatus.Rejected;
            quotation.AiReview.FailureReason = "AI flagged as non-quotation";
            await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
            return;
        }

        quotation.AiReview.Status = AiReviewStatus.Reviewed;
        await _quotationRepository.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);

        await ApplyLeanResultAsync(quotation, result);
    }
}
