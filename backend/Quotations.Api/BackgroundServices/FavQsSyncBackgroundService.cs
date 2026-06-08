using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;

namespace Quotations.Api.BackgroundServices;

public class FavQsSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FavQsSyncOptions _options;
    private readonly ILogger<FavQsSyncBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public FavQsSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<FavQsSyncOptions> options,
        ILogger<FavQsSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("FavQs sync is disabled (no API key configured).");
            return;
        }

        _logger.LogInformation("FavQs sync service started.");
        await RunSyncCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            await RunSyncCycleAsync(stoppingToken);
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncRepo = scope.ServiceProvider.GetRequiredService<IFavQsSyncRepository>();
        var favQsService = scope.ServiceProvider.GetRequiredService<FavQsService>();

        var running = await syncRepo.GetRunningAsync();
        if (running != null)
        {
            _logger.LogInformation("FavQs sync already running (started {StartedAt}), skipping.", running.StartedAt);
            return;
        }

        var last = await syncRepo.GetLastAsync();

        // Decide whether to run
        int startPage = 1;
        if (last?.Status == FavQsSyncStatus.Completed)
        {
            var daysSince = (DateTime.UtcNow - (last.CompletedAt ?? DateTime.MinValue)).TotalDays;
            if (daysSince < _options.ReSyncIntervalDays) return;
        }
        else if (last?.Status == FavQsSyncStatus.Failed)
        {
            // Resume from where we left off
            startPage = (last.ResumePage ?? 0) + 1;
        }

        await RunSyncAsync(startPage, syncRepo, favQsService, ct);
    }

    private async Task RunSyncAsync(
        int startPage,
        IFavQsSyncRepository syncRepo,
        FavQsService favQsService,
        CancellationToken ct)
    {
        var record = await syncRepo.CreateAsync(new FavQsSyncRecord
        {
            Status = FavQsSyncStatus.Running,
            StartedAt = DateTime.UtcNow,
            ResumePage = startPage > 1 ? startPage - 1 : null
        });

        if (startPage > 1)
            _logger.LogInformation("Resuming FavQs sync from page {Page} (id={Id}).", startPage, record.Id);
        else
            _logger.LogInformation("Starting FavQs sync (id={Id}).", record.Id);

        try
        {
            await foreach (var _ in favQsService.RunSyncAsync(record, ct, startPage).WithCancellation(ct))
            {
                if (record.PagesProcessed % 50 == 0)
                {
                    await syncRepo.UpdateAsync(record);
                    _logger.LogInformation(
                        "FavQs sync progress: {Pages} pages, {Inserted} inserted, {Skipped} skipped.",
                        record.PagesProcessed, record.QuotesInserted, record.QuotesSkipped);
                }
            }

            record.Status = FavQsSyncStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "FavQs sync completed: {Pages} pages, {Inserted} inserted, {Skipped} skipped.",
                record.PagesProcessed, record.QuotesInserted, record.QuotesSkipped);
        }
        catch (OperationCanceledException)
        {
            record.Status = FavQsSyncStatus.Failed;
            record.ErrorMessage = "Cancelled.";
            _logger.LogWarning("FavQs sync cancelled at page {Page}.", record.ResumePage);
        }
        catch (Exception ex)
        {
            record.Status = FavQsSyncStatus.Failed;
            record.ErrorMessage = ex.Message;
            _logger.LogError(ex, "FavQs sync failed.");
        }
        finally
        {
            await syncRepo.UpdateAsync(record);
        }
    }
}
