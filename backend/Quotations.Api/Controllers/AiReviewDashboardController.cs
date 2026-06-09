using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;
using System;
using System.Linq;

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
    private readonly IAnthropicService _anthropic;
    private readonly IAiBatchJobRepository _batchJobs;
    private readonly ILogger<AiReviewDashboardController> _logger;

    public AiReviewDashboardController(
        IQuotationRepository quotations,
        IAiReviewErrorRepository errors,
        AiReviewRuntimeSettings runtimeSettings,
        AiReviewService aiReviewService,
        IAiReviewQueueService queueService,
        IAnthropicService anthropic,
        IAiBatchJobRepository batchJobs,
        ILogger<AiReviewDashboardController> logger)
    {
        _quotations = quotations;
        _errors = errors;
        _runtimeSettings = runtimeSettings;
        _aiReviewService = aiReviewService;
        _queueService = queueService;
        _anthropic = anthropic;
        _batchJobs = batchJobs;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var counts = await _quotations.GetAiReviewCountsByStatusAsync();
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
            aiChangesApplied = q.AiRevisions.Count > 0
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
            data = new
            {
                autoEnqueueEnabled = _runtimeSettings.AutoEnqueueEnabled,
                autoProcessingEnabled = _runtimeSettings.AutoProcessingEnabled
            }
        });
    }

    [HttpPost("settings/auto-enqueue")]
    public IActionResult SetAutoEnqueue([FromBody] SetAutoEnqueueRequest request)
    {
        _runtimeSettings.AutoEnqueueEnabled = request.Enabled;
        return Ok(new { success = true, data = new { autoEnqueueEnabled = _runtimeSettings.AutoEnqueueEnabled } });
    }

    [HttpPost("settings/auto-processing")]
    public IActionResult SetAutoProcessing([FromBody] SetAutoProcessingRequest request)
    {
        _runtimeSettings.AutoProcessingEnabled = request.Enabled;
        return Ok(new { success = true, data = new { autoProcessingEnabled = _runtimeSettings.AutoProcessingEnabled } });
    }

    [HttpPost("queue/{quotationId}")]
    public async Task<IActionResult> EnqueueOne(string quotationId)
    {
        var result = await _queueService.EnqueueAsync(quotationId);
        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = new { quotationId = result.QuotationId } });
    }

    [HttpPost("queue/all-unreviewed")]
    public async Task<IActionResult> EnqueueAllUnreviewed()
    {
        var result = await _queueService.EnqueueAllUnreviewedAsync();
        return Ok(new { success = true, message = result.Message, data = new { enqueued = result.Enqueued, skipped = result.Skipped } });
    }

    [HttpGet("request-preview/{quotationId}")]
    public async Task<IActionResult> GetRequestPreview(string quotationId)
    {
        var preview = await _queueService.GetRequestPreviewAsync(quotationId);
        if (preview == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        return Ok(new { success = true, data = preview });
    }

    [HttpPost("process/{quotationId}")]
    public async Task<IActionResult> ProcessNow(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });

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
                reviewedAt = updated?.AiReview?.ReviewedAt,
                modelUsed = updated?.AiReview?.ModelUsed,
                changesApplied = updated?.AiRevisions.Count > 0
            }
        });
    }

    [HttpGet("detail/{quotationId}")]
    public async Task<IActionResult> GetDetail(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        var review = quotation.AiReview;

        return Ok(new
        {
            success = true,
            data = new
            {
                quotationId = quotation.Id,
                text = quotation.Text,
                authorName = quotation.Author.Name,
                sourceTitle = quotation.Source.Title,
                originalText = quotation.OriginalText,
                originalAuthorName = quotation.OriginalAuthorName,
                originalSourceTitle = quotation.OriginalSourceTitle,
                tags = quotation.Tags,
                modelUsed = review?.ModelUsed,
                reviewedAt = review?.ReviewedAt,
                revisions = quotation.AiRevisions
                    .OrderByDescending(r => r.AppliedAt)
                    .Select(r => new
                    {
                        appliedAt = r.AppliedAt,
                        modelUsed = r.ModelUsed,
                        changes = r.Changes.Select(c => new
                        {
                            field = c.Field,
                            previousValue = c.PreviousValue,
                            newValue = c.NewValue
                        })
                    })
            }
        });
    }

    [HttpGet("revisions/{quotationId}")]
    public async Task<IActionResult> GetRevisions(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        var revisions = quotation.AiRevisions
            .OrderByDescending(r => r.AppliedAt)
            .Select(r => new
            {
                appliedAt = r.AppliedAt,
                modelUsed = r.ModelUsed,
                changes = r.Changes.Select(c => new
                {
                    field = c.Field,
                    previousValue = c.PreviousValue,
                    newValue = c.NewValue
                })
            });

        return Ok(new { success = true, data = revisions });
    }

    [HttpPost("revisions/{quotationId}/revert-last")]
    public async Task<IActionResult> RevertLastRevision(string quotationId)
    {
        var quotation = await _quotations.GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });

        if (quotation.AiRevisions.Count == 0)
            return BadRequest(new { success = false, message = "No AI revisions to revert" });

        var last = quotation.AiRevisions[^1];

        foreach (var change in last.Changes)
        {
            switch (change.Field)
            {
                case "Text":   quotation.Text = change.PreviousValue; break;
                case "Author": quotation.Author.Name = change.PreviousValue; break;
                case "Source": quotation.Source.Title = change.PreviousValue; break;
                case "Tags":
                    quotation.Tags = change.PreviousValue
                        .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                        .ToList();
                    break;
            }
        }

        quotation.AiRevisions.RemoveAt(quotation.AiRevisions.Count - 1);
        await _quotations.UpdateQuotationAsync(quotation);

        return Ok(new { success = true, message = "Last AI revision reverted" });
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

    /// <summary>
    /// Submit up to 10,000 unreviewed quotations to the Anthropic Batch API (lean single-pass).
    /// Results arrive asynchronously (minutes to hours) at ~50% of standard pricing.
    /// </summary>
    [HttpPost("batch/submit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SubmitBatch([FromQuery] int limit = 10000)
    {
        limit = Math.Clamp(limit, 1, 10000);

        var quotations = await _quotations.GetUnreviewedForBatchAsync(limit);
        if (quotations.Count == 0)
            return Ok(new { success = true, message = "No unreviewed quotations to submit.", data = new { submitted = 0 } });

        // Pre-filter obvious garbage before spending API tokens on it
        var garbage = quotations.Where(q => QuotationGarbageFilter.IsLikelyGarbage(q.Text)).ToList();
        if (garbage.Count > 0)
        {
            await _quotations.BulkSetAiReviewStatusAsync(
                garbage.Select(q => q.Id).ToList(),
                AiReviewStatus.Rejected);
            _logger.LogInformation("Pre-filtered {Count} garbage quotations before batch submit", garbage.Count);
        }

        quotations = quotations.Where(q => !QuotationGarbageFilter.IsLikelyGarbage(q.Text)).ToList();
        if (quotations.Count == 0)
            return Ok(new { success = true, message = "All candidates were pre-filtered as garbage.", data = new { submitted = 0, preFiltered = garbage.Count } });

        var requests = quotations.Select(q => (
            QuotationId: q.Id,
            Text: q.Text,
            AuthorName: q.Author.Name,
            SourceTitle: q.Source.Title,
            SourceType: q.Source.Type.ToString()
        ));

        var batchResult = await _anthropic.SubmitLeanBatchAsync(requests);

        var job = new AiBatchJob
        {
            AnthropicBatchId = batchResult.AnthropicBatchId,
            Phase = AiBatchJobPhase.Review,
            Status = AiBatchJobStatus.Submitted,
            QuotationIds = quotations.Select(q => q.Id).ToList(),
            TotalCount = batchResult.RequestCount,
            ModelUsed = string.Empty,
            SubmittedAt = DateTime.UtcNow
        };
        await _batchJobs.CreateAsync(job);

        await _quotations.BulkSetAiReviewStatusAsync(job.QuotationIds, AiReviewStatus.BatchPending);

        return Ok(new
        {
            success = true,
            message = $"Submitted {batchResult.RequestCount} quotations to Anthropic Batch API.",
            data = new
            {
                jobId = job.Id,
                anthropicBatchId = batchResult.AnthropicBatchId,
                submitted = batchResult.RequestCount
            }
        });
    }

    [HttpGet("batch/jobs")]
    public async Task<IActionResult> GetBatchJobs([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var jobs = await _batchJobs.GetRecentAsync(limit);

        var items = jobs.Select(j => new
        {
            jobId = j.Id,
            anthropicBatchId = j.AnthropicBatchId,
            status = j.Status.ToString(),
            totalCount = j.TotalCount,
            succeededCount = j.SucceededCount,
            failedCount = j.FailedCount,
            modelUsed = j.ModelUsed,
            submittedAt = j.SubmittedAt,
            completedAt = j.CompletedAt,
            errorMessage = j.ErrorMessage
        });

        return Ok(new { success = true, data = items });
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

public record SetAutoEnqueueRequest(bool Enabled);
public record SetAutoProcessingRequest(bool Enabled);
