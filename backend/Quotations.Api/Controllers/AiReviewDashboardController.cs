using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotations.Api.Models;
using Quotations.Api.Repositories;

namespace Quotations.Api.Controllers;

[ApiController]
[Route("api/v1/ai-review")]
[Authorize(Roles = "Reviewer,Admin")]
public class AiReviewDashboardController : ControllerBase
{
    private readonly IQuotationRepository _quotations;
    private readonly IAiReviewErrorRepository _errors;

    public AiReviewDashboardController(
        IQuotationRepository quotations,
        IAiReviewErrorRepository errors)
    {
        _quotations = quotations;
        _errors = errors;
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
