using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Repositories;
using Quotations.Api.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quotations.Api.BackgroundServices;

public class AiReviewBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiReviewBackgroundService> _logger;
    private readonly AiReviewOptions _options;

    public AiReviewBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AiReviewOptions> options,
        ILogger<AiReviewBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Review background service started. Polling every {Interval}s when idle.",
            _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);

                if (processed == 0)
                {
                    // Queue is empty — enter idle mode
                    _logger.LogDebug("No pending AI reviews found. Sleeping for {Interval}s.", _options.PollingIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
                }
                // If we processed a full batch, loop immediately to check for more
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AI review background service. Retrying in 60s.");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        _logger.LogInformation("AI Review background service stopped.");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var quotationRepository = scope.ServiceProvider.GetRequiredService<IQuotationRepository>();
        var aiReviewService = scope.ServiceProvider.GetRequiredService<AiReviewService>();

        var pending = await quotationRepository.GetPendingAiReviewsAsync(_options.BatchSize);
        if (pending.Count == 0) return 0;

        _logger.LogInformation("Processing batch of {Count} pending AI reviews.", pending.Count);

        var semaphore = new SemaphoreSlim(_options.ConcurrentRequests, _options.ConcurrentRequests);

        var tasks = pending.Select(async quotation =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                await aiReviewService.ReviewQuotationAsync(quotation);
                if (_options.DelayBetweenRequestsMs > 0)
                    await Task.Delay(_options.DelayBetweenRequestsMs, stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return pending.Count;
    }
}
