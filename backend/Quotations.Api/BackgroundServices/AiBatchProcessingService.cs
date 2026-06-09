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
        _logger.LogInformation("Batch {BatchId} ({Phase}) status: {Status}",
            job.AnthropicBatchId, job.Phase, status.ProcessingStatus);

        if (status.ProcessingStatus != "ended")
        {
            job.Status = AiBatchJobStatus.InProgress;
            await batchJobRepo.UpdateAsync(job);
            return;
        }

        _logger.LogInformation("Batch {BatchId} ended. Succeeded: {S}, Errored: {E}, Expired: {X}",
            job.AnthropicBatchId, status.Succeeded, status.Errored, status.Expired);

        var results = await anthropic.GetBatchResultsAsync(job.AnthropicBatchId);
        var (succeeded, failed) = await ProcessLeanResultsAsync(results, job, quotationRepo, aiReviewService);

        // Mark any quotations still BatchPending that had no result as Failed
        var resultIds = new HashSet<string>(results.Select(r => r.CustomId));
        foreach (var qId in job.QuotationIds.Where(id => !resultIds.Contains(id)))
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

        _logger.LogInformation("Batch job {JobId} complete. Succeeded: {S}, Failed: {F}", job.Id, succeeded, failed);
    }

    private async Task<(int succeeded, int failed)> ProcessLeanResultsAsync(
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
                    _logger.LogWarning("Batch result references unknown quotation {Id}", result.CustomId);
                    failed++;
                    continue;
                }

                if (!result.Succeeded || string.IsNullOrWhiteSpace(result.ContentText))
                {
                    _logger.LogWarning("Batch result failed for quotation {Id}: {Error}", result.CustomId, result.ErrorType);
                    quotation.AiReview ??= new AiReview();
                    quotation.AiReview.Status = AiReviewStatus.Failed;
                    quotation.AiReview.FailureReason = result.ErrorType ?? "Batch request failed";
                    await quotationRepo.UpdateAiReviewAsync(quotation.Id, quotation.AiReview);
                    failed++;
                    continue;
                }

                await aiReviewService.ApplyLeanBatchResultAsync(quotation, result.ContentText, job.ModelUsed);
                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying batch result for quotation {Id}", result.CustomId);
                failed++;
            }
        }

        return (succeeded, failed);
    }
}
