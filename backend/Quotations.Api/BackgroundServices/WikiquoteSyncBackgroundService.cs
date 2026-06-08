using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;
using Quotations.Api.Services;

namespace Quotations.Api.BackgroundServices;

public class WikiquoteSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WikiquoteSyncOptions _options;
    private readonly ILogger<WikiquoteSyncBackgroundService> _logger;

    // Check every hour whether a sync is due
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public WikiquoteSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<WikiquoteSyncOptions> options,
        ILogger<WikiquoteSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Wikiquote sync is disabled.");
            return;
        }

        _logger.LogInformation("Wikiquote sync service started.");

        // On startup, check immediately
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
        var syncRepo = scope.ServiceProvider.GetRequiredService<IWikiquoteSyncRepository>();
        var wikiquoteService = scope.ServiceProvider.GetRequiredService<WikiquoteService>();

        // Don't start if one is already running
        var running = await syncRepo.GetRunningAsync();
        if (running != null)
        {
            _logger.LogInformation("Wikiquote sync already running (started {StartedAt}), skipping.", running.StartedAt);
            return;
        }

        var lastFull = await syncRepo.GetLastCompletedAsync(WikiquoteSyncType.Full);

        if (lastFull is null)
        {
            // Resume a cancelled partial run if one exists
            var lastAttempt = await syncRepo.GetLastAsync(WikiquoteSyncType.Full);
            var resumeToken = lastAttempt?.Status == WikiquoteSyncStatus.Failed
                ? lastAttempt.ContinueToken
                : null;
            await RunSyncAsync(WikiquoteSyncType.Full, null, resumeToken, syncRepo, wikiquoteService, ct);
            return;
        }

        var lastSync = await syncRepo.GetLastCompletedAsync();
        var daysSinceLast = (DateTime.UtcNow - (lastSync?.CompletedAt ?? DateTime.MinValue)).TotalDays;

        if (daysSinceLast >= _options.DeltaIntervalDays)
        {
            var deltaFrom = lastSync!.DeltaFromTimestamp ?? lastSync.StartedAt;
            await RunSyncAsync(WikiquoteSyncType.Delta, deltaFrom, null, syncRepo, wikiquoteService, ct);
        }
    }

    private async Task RunSyncAsync(
        WikiquoteSyncType syncType,
        DateTime? deltaFrom,
        string? resumeToken,
        IWikiquoteSyncRepository syncRepo,
        WikiquoteService wikiquoteService,
        CancellationToken ct)
    {
        var record = await syncRepo.CreateAsync(new WikiquoteSyncRecord
        {
            SyncType = syncType,
            Status = WikiquoteSyncStatus.Running,
            StartedAt = DateTime.UtcNow,
            DeltaFromTimestamp = deltaFrom
        });

        if (resumeToken != null)
            _logger.LogInformation("Resuming Wikiquote {SyncType} sync from token {Token} (id={Id}).", syncType, resumeToken, record.Id);
        else
            _logger.LogInformation("Starting Wikiquote {SyncType} sync (id={Id}).", syncType, record.Id);

        try
        {
            var syncStarted = DateTime.UtcNow;

            var stream = syncType == WikiquoteSyncType.Full
                ? wikiquoteService.RunFullSyncAsync(record, ct, resumeToken)
                : wikiquoteService.RunDeltaSyncAsync(deltaFrom!.Value, record, ct);

            await foreach (var _ in stream.WithCancellation(ct))
            {
                // Persist progress every 100 pages
                if (record.PagesProcessed % 100 == 0)
                {
                    await syncRepo.UpdateAsync(record);
                    _logger.LogInformation(
                        "Wikiquote sync progress: {Pages} pages, {Inserted} inserted, {Skipped} skipped.",
                        record.PagesProcessed, record.QuotesInserted, record.QuotesSkipped);
                }
            }

            record.Status = WikiquoteSyncStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            record.DeltaFromTimestamp = syncStarted;

            _logger.LogInformation(
                "Wikiquote {SyncType} sync completed: {Pages} pages, {Inserted} inserted, {Skipped} skipped.",
                syncType, record.PagesProcessed, record.QuotesInserted, record.QuotesSkipped);
        }
        catch (OperationCanceledException)
        {
            record.Status = WikiquoteSyncStatus.Failed;
            record.ErrorMessage = "Cancelled.";
            _logger.LogWarning("Wikiquote sync cancelled.");
        }
        catch (Exception ex)
        {
            record.Status = WikiquoteSyncStatus.Failed;
            record.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Wikiquote sync failed.");
        }
        finally
        {
            await syncRepo.UpdateAsync(record);
        }
    }
}
