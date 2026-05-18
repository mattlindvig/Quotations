using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quotations.Api.BackgroundServices;

public class AiBatchProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiBatchProcessingService> _logger;
    private readonly AiReviewOptions _options;

    // Fix batch: worst case is 3 requests per quote; stay well under the 10,000 Anthropic limit
    private const int FixBatchQuoteLimit = 3000;

    // Poll every 10 minutes — batches take minutes to hours to complete
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

    public AiBatchProcessingService(
        IServiceScopeFactory scopeFactory,
        IOptions<AiReviewOptions> options,
        ILogger<AiBatchProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Batch Processing service started. Polling every {Interval}m.", PollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingBatchesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AI batch processing service.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("AI Batch Processing service stopped.");
    }

    private async Task CheckPendingBatchesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchJobRepo    = scope.ServiceProvider.GetRequiredService<IAiBatchJobRepository>();
        var anthropic       = scope.ServiceProvider.GetRequiredService<IAnthropicService>();
        var quotationRepo   = scope.ServiceProvider.GetRequiredService<IQuotationRepository>();
        var aiReviewService = scope.ServiceProvider.GetRequiredService<AiReviewService>();

        var pendingJobs = await batchJobRepo.GetPendingJobsAsync();
        if (pendingJobs.Count == 0) return;

        _logger.LogInformation("Checking {Count} pending batch job(s).", pendingJobs.Count);

        foreach (var job in pendingJobs)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await ProcessJobAsync(job, batchJobRepo, anthropic, quotationRepo, aiReviewService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch job {JobId} (Anthropic: {BatchId})", job.Id, job.AnthropicBatchId);
                job.Status = AiBatchJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await batchJobRepo.UpdateAsync(job);
            }
        }
    }

    private async Task ProcessJobAsync(
        AiBatchJob job,
        IAiBatchJobRepository batchJobRepo,
        IAnthropicService anthropic,
        IQuotationRepository quotationRepo,
        AiReviewService aiReviewService)
    {
        var status = await anthropic.GetBatchStatusAsync(job.AnthropicBatchId);
        _logger.LogInformation("Batch {BatchId} ({Phase}) status: {Status}", job.AnthropicBatchId, job.Phase, status.ProcessingStatus);

        if (status.ProcessingStatus != "ended")
        {
            job.Status = AiBatchJobStatus.InProgress;
            await batchJobRepo.UpdateAsync(job);
            return;
        }

        _logger.LogInformation("Batch {BatchId} ended. Succeeded: {S}, Errored: {E}, Expired: {X}",
            job.AnthropicBatchId, status.Succeeded, status.Errored, status.Expired);

        var results = await anthropic.GetBatchResultsAsync(job.AnthropicBatchId);

        int succeeded = 0, failed = 0;

        if (job.Phase == AiBatchJobPhase.Triage)
        {
            (succeeded, failed) = await ProcessTriageResultsAsync(results, job, quotationRepo, aiReviewService);

            // Auto-submit a fix batch for any quotes that came back FixPending
            await SubmitFixBatchIfNeededAsync(batchJobRepo, anthropic, quotationRepo);
        }
        else
        {
            (succeeded, failed) = await ProcessFixResultsAsync(results, job, quotationRepo, aiReviewService);
        }

        // Mark any quotations still BatchPending that had no result as Failed
        var resultQuotationIds = new HashSet<string>(results.Select(r => ExtractQuotationId(r.CustomId)));
        foreach (var qId in job.QuotationIds.Where(id => !resultQuotationIds.Contains(id)))
        {
            var quotation = await quotationRepo.GetQuotationByIdAsync(qId);
            if (quotation?.AiReview?.Status == AiReviewStatus.BatchPending)
            {
                quotation.AiReview.Status = AiReviewStatus.Failed;
                quotation.AiReview.FailureReason = "No result in batch response";
                await quotationRepo.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                failed++;
            }
        }

        job.Status = AiBatchJobStatus.Completed;
        job.SucceededCount = succeeded;
        job.FailedCount = failed;
        job.CompletedAt = DateTime.UtcNow;
        await batchJobRepo.UpdateAsync(job);

        _logger.LogInformation("Batch job {JobId} ({Phase}) complete. Succeeded: {S}, Failed: {F}", job.Id, job.Phase, succeeded, failed);
    }

    private async Task<(int succeeded, int failed)> ProcessTriageResultsAsync(
        List<BatchMessageResult> results,
        AiBatchJob job,
        IQuotationRepository quotationRepo,
        AiReviewService aiReviewService)
    {
        int succeeded = 0, failed = 0;

        foreach (var result in results)
        {
            try
            {
                var quotation = await quotationRepo.GetQuotationByIdAsync(result.CustomId);
                if (quotation == null)
                {
                    _logger.LogWarning("Triage batch result references unknown quotation {Id}", result.CustomId);
                    failed++;
                    continue;
                }

                if (!result.Succeeded || string.IsNullOrWhiteSpace(result.ContentText))
                {
                    _logger.LogWarning("Triage batch result failed for quotation {Id}: {Error}", result.CustomId, result.ErrorType);
                    quotation.AiReview ??= new AiReview();
                    quotation.AiReview.Status = AiReviewStatus.Failed;
                    quotation.AiReview.FailureReason = result.ErrorType ?? "Triage batch request failed";
                    await quotationRepo.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                    failed++;
                    continue;
                }

                await aiReviewService.ApplyTriageBatchResultAsync(quotation, result.ContentText, job.ModelUsed);
                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying triage batch result for quotation {Id}", result.CustomId);
                failed++;
            }
        }

        return (succeeded, failed);
    }

    private async Task<(int succeeded, int failed)> ProcessFixResultsAsync(
        List<BatchMessageResult> results,
        AiBatchJob job,
        IQuotationRepository quotationRepo,
        AiReviewService aiReviewService)
    {
        int succeeded = 0, failed = 0;

        // Group results by quotation ID (each quote may have up to 3 fix results)
        var byQuotation = results
            .Where(r => r.Succeeded && !string.IsNullOrWhiteSpace(r.ContentText))
            .GroupBy(r => ExtractQuotationId(r.CustomId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (quotationId, quotationResults) in byQuotation)
        {
            try
            {
                var quotation = await quotationRepo.GetQuotationByIdAsync(quotationId);
                if (quotation == null)
                {
                    _logger.LogWarning("Fix batch result references unknown quotation {Id}", quotationId);
                    failed++;
                    continue;
                }

                var fieldResults = quotationResults
                    .Select(r => (Field: ExtractField(r.CustomId), ContentText: r.ContentText!))
                    .Where(r => !string.IsNullOrEmpty(r.Field))
                    .ToList();

                await aiReviewService.ApplyFixBatchResultsAsync(quotation, fieldResults, job.ModelUsed);
                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying fix batch results for quotation {Id}", quotationId);
                failed++;
            }
        }

        // Count any entirely failed fix results
        failed += results.Count(r => !r.Succeeded);

        return (succeeded, failed);
    }

    private async Task SubmitFixBatchIfNeededAsync(
        IAiBatchJobRepository batchJobRepo,
        IAnthropicService anthropic,
        IQuotationRepository quotationRepo)
    {
        var fixPending = await quotationRepo.GetFixPendingForBatchAsync(FixBatchQuoteLimit);
        if (fixPending.Count == 0) return;

        _logger.LogInformation("Auto-submitting fix batch for {Count} FixPending quotations.", fixPending.Count);

        const int FixThreshold = 8;
        var fixRequests = fixPending
            .SelectMany(q => new[]
            {
                q.AiReview?.QuoteAccuracy?.Score < FixThreshold
                    ? ("quote",  q) : (null, q),
                q.AiReview?.AttributionAccuracy?.Score < FixThreshold
                    ? ("author", q) : (null, q),
                q.AiReview?.SourceAccuracy?.Score < FixThreshold
                    ? ("source", q) : (null, q),
            })
            .Where(x => x.Item1 != null)
            .Select(x => (
                QuotationId: x.q.Id,
                Field: x.Item1!,
                Text: x.q.Text,
                AuthorName: x.q.Author.Name,
                SourceTitle: x.q.Source.Title,
                SourceType: x.q.Source.Type.ToString()
            ))
            .ToList();

        if (fixRequests.Count == 0) return;

        var batchResult = await anthropic.SubmitFixBatchAsync(fixRequests);

        var job = new AiBatchJob
        {
            AnthropicBatchId = batchResult.AnthropicBatchId,
            Phase = AiBatchJobPhase.Fix,
            Status = AiBatchJobStatus.Submitted,
            QuotationIds = fixPending.Select(q => q.Id).ToList(),
            TotalCount = batchResult.RequestCount,
            ModelUsed = string.Empty,
            SubmittedAt = DateTime.UtcNow
        };
        await batchJobRepo.CreateAsync(job);

        await quotationRepo.BulkSetAiReviewStatusAsync(fixPending.Select(q => q.Id), AiReviewStatus.BatchPending);

        _logger.LogInformation("Fix batch {BatchId} submitted with {Count} requests.", batchResult.AnthropicBatchId, batchResult.RequestCount);
    }

    // custom_id format: "{quotationId}" for triage, "{quotationId}:{field}" for fix
    private static string ExtractQuotationId(string customId)
    {
        var colon = customId.IndexOf(':');
        return colon >= 0 ? customId[..colon] : customId;
    }

    private static string ExtractField(string customId)
    {
        var colon = customId.IndexOf(':');
        return colon >= 0 ? customId[(colon + 1)..] : string.Empty;
    }
}
