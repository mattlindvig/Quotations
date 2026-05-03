using Microsoft.Extensions.Logging;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quotations.Api.Services;

public record EnqueueResult(bool Success, string Message, string? QuotationId = null);
public record EnqueueBatchResult(int Enqueued, int Skipped, string Message);

public class AiRequestPreviewDto
{
    public string QuotationId { get; set; } = string.Empty;
    public string QuotationText { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty;
}

public interface IAiReviewQueueService
{
    Task<EnqueueResult> EnqueueAsync(string quotationId);
    Task<EnqueueBatchResult> EnqueueAllUnreviewedAsync();
    Task<AiRequestPreviewDto?> GetRequestPreviewAsync(string quotationId);
}

public class AiReviewQueueService : IAiReviewQueueService
{
    private readonly IQuotationRepository _quotations;
    private readonly IAnthropicService _anthropic;
    private readonly ILogger<AiReviewQueueService> _logger;

    public AiReviewQueueService(
        IQuotationRepository quotations,
        IAnthropicService anthropic,
        ILogger<AiReviewQueueService> logger)
    {
        _quotations = quotations;
        _anthropic = anthropic;
        _logger = logger;
    }

    public async Task<EnqueueResult> EnqueueAsync(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return new EnqueueResult(false, "Quotation not found");

        if (quotation.AiReview?.Status == AiReviewStatus.InProgress)
            return new EnqueueResult(false, "Quotation is currently being processed — wait for it to finish");

        quotation.AiReview ??= new AiReview();
        quotation.AiReview.Status = AiReviewStatus.Pending;
        quotation.AiReview.RetryCount = 0;
        quotation.AiReview.FailureReason = null;
        quotation.AiReview.LastAttemptAt = null;

        await _quotations.UpdateAiReviewAsync(quotationId, quotation.AiReview);
        _logger.LogInformation("Enqueued quotation {QuotationId} for AI review", quotationId);

        return new EnqueueResult(true, "Quotation enqueued for AI review", quotationId);
    }

    public async Task<EnqueueBatchResult> EnqueueAllUnreviewedAsync()
    {
        int enqueued = 0, skipped = 0;
        int page = 1;
        const int pageSize = 100;

        while (true)
        {
            var (items, total) = await _quotations.GetUnreviewedForAiAsync(page, pageSize);
            if (items.Count == 0) break;

            foreach (var q in items)
            {
                var result = await EnqueueAsync(q.Id);
                if (result.Success) enqueued++; else skipped++;
            }

            if (page * pageSize >= total) break;
            page++;
        }

        _logger.LogInformation("Batch enqueue complete: {Enqueued} enqueued, {Skipped} skipped", enqueued, skipped);
        return new EnqueueBatchResult(enqueued, skipped, $"Enqueued {enqueued} quotations for AI review ({skipped} skipped)");
    }

    public async Task<AiRequestPreviewDto?> GetRequestPreviewAsync(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null) return null;

        var preview = _anthropic.BuildRequestPreview(
            quotation.Text,
            quotation.Author.Name,
            null,
            quotation.Source.Title,
            quotation.Source.Type.ToString(),
            null);

        return new AiRequestPreviewDto
        {
            QuotationId = quotation.Id,
            QuotationText = quotation.Text,
            AuthorName = quotation.Author.Name,
            SourceTitle = quotation.Source.Title,
            SourceType = quotation.Source.Type.ToString(),
            CurrentStatus = quotation.AiReview?.Status.ToString() ?? "NotReviewed",
            Model = preview.Model,
            MaxTokens = preview.MaxTokens,
            Prompt = preview.Prompt,
            RequestJson = preview.RequestJson
        };
    }
}
