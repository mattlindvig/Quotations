using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/ai-review")]
[Authorize(Roles = "Reviewer,Admin")]
public class AiReviewDashboardController : ControllerBase
{
    private readonly IQuotationRepository _quotations;
    private readonly IAiReviewErrorRepository _errors;
    private readonly AiReviewRuntimeSettings _runtimeSettings;
    private readonly AiReviewService _aiReviewService;
    private readonly IAiReviewQueueService _queueService;

    public AiReviewDashboardController(
        IQuotationRepository quotations,
        IAiReviewErrorRepository errors,
        AiReviewRuntimeSettings runtimeSettings,
        AiReviewService aiReviewService,
        IAiReviewQueueService queueService)
    {
        _quotations = quotations;
        _errors = errors;
        _runtimeSettings = runtimeSettings;
        _aiReviewService = aiReviewService;
        _queueService = queueService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var counts = await _quotations.GetAiReviewCountsByStatusAsync();
        var (quoteAvg, attrAvg, srcAvg) = await _quotations.GetAverageAiScoresAsync();
        var errorCount = await _errors.CountAsync();

        var allStatuses = new[] { "NotReviewed", "Pending", "InProgress", "Reviewed", "Failed" };
        var normalized = allStatuses.ToDictionary(s => s, s => counts.GetValueOrDefault(s, 0));

        return Ok(new
        {
            success = true,
            data = new
            {
                counts = normalized,
                total = normalized.Values.Sum(),
                averageScores = new
                {
                    quoteAccuracy = quoteAvg.HasValue ? Math.Round(quoteAvg.Value, 1) : (double?)null,
                    attribution = attrAvg.HasValue ? Math.Round(attrAvg.Value, 1) : (double?)null,
                    source = srcAvg.HasValue ? Math.Round(srcAvg.Value, 1) : (double?)null,
                },
                errorCount
            }
        });
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var quotations = await _quotations.GetRecentlyAiReviewedAsync(limit);

        var items = quotations.Select(q => new
        {
            quotationId = q.Id,
            text = q.Text.Length > 120 ? q.Text[..120] + "…" : q.Text,
            authorName = q.Author.Name,
            reviewedAt = q.AiReview.ReviewedAt,
            modelUsed = q.AiReview.ModelUsed,
            summary = q.AiReview.Summary,
            scores = new
            {
                quoteAccuracy = q.AiReview.QuoteAccuracy?.Score,
                attribution = q.AiReview.AttributionAccuracy?.Score,
                source = q.AiReview.SourceAccuracy?.Score,
            }
        });

        return Ok(new { success = true, data = items });
    }

    [HttpGet("unreviewed")]
    public async Task<IActionResult> GetUnreviewed([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _quotations.GetUnreviewedForAiAsync(page, pageSize);

        var rows = items.Select(q => new
        {
            quotationId = q.Id,
            text = q.Text.Length > 140 ? q.Text[..140] + "…" : q.Text,
            authorName = q.Author.Name,
            sourcTitle = q.Source.Title,
            submittedAt = q.SubmittedAt,
            status = q.AiReview?.Status.ToString() ?? "NotReviewed"
        });

        return Ok(new
        {
            success = true,
            data = new
            {
                items = rows,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount = total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize),
                    hasNext = page * pageSize < total,
                    hasPrevious = page > 1
                }
            }
        });
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            success = true,
            data = new { autoProcessingEnabled = _runtimeSettings.AutoProcessingEnabled }
        });
    }

    [HttpPost("settings/auto-processing")]
    public IActionResult SetAutoProcessing([FromBody] SetAutoProcessingRequest request)
    {
        _runtimeSettings.AutoProcessingEnabled = request.Enabled;
        return Ok(new
        {
            success = true,
            data = new { autoProcessingEnabled = _runtimeSettings.AutoProcessingEnabled }
        });
    }

    /// <summary>
    /// Add a specific quotation to the AI review queue (sets it to Pending for background processing).
    /// </summary>
    [HttpPost("queue/{quotationId}")]
    public async Task<IActionResult> EnqueueOne(string quotationId)
    {
        var result = await _queueService.EnqueueAsync(quotationId);
        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = new { quotationId = result.QuotationId } });
    }

    /// <summary>
    /// Queue all quotations that have not yet been AI reviewed (NotReviewed status).
    /// </summary>
    [HttpPost("queue/all-unreviewed")]
    public async Task<IActionResult> EnqueueAllUnreviewed()
    {
        var result = await _queueService.EnqueueAllUnreviewedAsync();
        return Ok(new { success = true, message = result.Message, data = new { enqueued = result.Enqueued, skipped = result.Skipped } });
    }

    /// <summary>
    /// Preview the exact request that would be sent to Anthropic for a given quotation, without executing it.
    /// Useful for testing and debugging the prompt before committing API calls.
    /// </summary>
    [HttpGet("request-preview/{quotationId}")]
    public async Task<IActionResult> GetRequestPreview(string quotationId)
    {
        var preview = await _queueService.GetRequestPreviewAsync(quotationId);
        if (preview == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        return Ok(new { success = true, data = preview });
    }

    /// <summary>
    /// Immediately run AI review on a specific quotation (resets + processes synchronously).
    /// </summary>
    [HttpPost("process/{quotationId}")]
    public async Task<IActionResult> ProcessNow(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        // Clear any prior error record and reset so ReviewQuotationAsync starts fresh
        await _errors.DeleteByQuotationIdAsync(quotationId);
        await _quotations.ResetAiReviewAsync(quotationId);
        quotation.AiReview ??= new AiReview();
        quotation.AiReview.Status = AiReviewStatus.NotReviewed;
        quotation.AiReview.RetryCount = 0;
        quotation.AiReview.FailureReason = null;

        await _aiReviewService.ReviewQuotationAsync(quotation);

        var updated = await _quotations.GetQuotationByIdAsync(quotationId);
        return Ok(new
        {
            success = true,
            data = new
            {
                status = updated?.AiReview?.Status.ToString(),
                scores = updated?.AiReview == null ? null : new
                {
                    quoteAccuracy = updated.AiReview.QuoteAccuracy?.Score,
                    attribution = updated.AiReview.AttributionAccuracy?.Score,
                    source = updated.AiReview.SourceAccuracy?.Score,
                }
            }
        });
    }

    [HttpPost("errors/{quotationId}/requeue")]
    public async Task<IActionResult> RequeueOne(string quotationId)
    {
        var reset = await _quotations.ResetAiReviewAsync(quotationId);
        if (!reset) return NotFound(new { success = false, message = "Quotation not found" });

        await _errors.DeleteByQuotationIdAsync(quotationId);
        return Ok(new { success = true, message = "Quotation requeued for AI review" });
    }

    [HttpPost("errors/requeue-all")]
    public async Task<IActionResult> RequeueAll()
    {
        var requeued = await _quotations.ResetAllFailedAiReviewsAsync();
        await _errors.DeleteAllAsync();
        return Ok(new { success = true, data = new { requeued } });
    }

    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var errors = await _errors.GetAllAsync(page, pageSize);
        var total = await _errors.CountAsync();

        var items = errors.Select(e => new
        {
            quotationId = e.QuotationId,
            text = e.QuotationText.Length > 120 ? e.QuotationText[..120] + "…" : e.QuotationText,
            authorName = e.AuthorName,
            lastError = e.LastError,
            retryCount = e.RetryCount,
            failedAt = e.FailedAt
        });

        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount = total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                }
            }
        });
    }
}

public record SetAutoProcessingRequest(bool Enabled);
