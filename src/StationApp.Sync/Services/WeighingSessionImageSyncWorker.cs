using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;

namespace StationApp.Sync.Services;

public sealed class WeighingSessionImageSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeighingSessionImageSyncWorker> _logger;
    private int _intervalSeconds = 30;

    public WeighingSessionImageSyncWorker(IServiceScopeFactory scopeFactory, ILogger<WeighingSessionImageSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeighingSessionImageSyncWorker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WeighingSessionImageSyncWorker cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var imageRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionImageRepository>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var syncClient = scope.ServiceProvider.GetRequiredService<IWeighingSessionImageSyncClient>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var intervalRaw = await appConfig.GetValueAsync(AppConfigKeys.SyncIntervalSeconds, ct)
            ?? await appConfig.GetValueAsync("sync_interval", ct);
        if (int.TryParse(intervalRaw, out var intervalValue) && intervalValue > 0)
        {
            _intervalSeconds = intervalValue;
        }

        var images = await imageRepo.GetPendingSyncAsync(5, ct);
        foreach (var image in images)
        {
            image.SyncStatus = ImageSyncStatus.SYNCING;
            image.LastSyncAttemptAt = clock.NowLocal;
            await imageRepo.UpdateAsync(image, ct);
            await uow.SaveChangesAsync(ct);

            var result = await syncClient.PushImageAsync(image, ct);
            if (result.Success)
            {
                image.SyncStatus = ImageSyncStatus.SYNCED;
                image.LastSyncSuccessAt = clock.NowLocal;
                image.LastSyncError = null;
            }
            else
            {
                image.SyncStatus = ImageSyncStatus.FAILED;
                image.LastSyncError = result.ErrorMessage;
                image.RetryCount++;
            }

            image.UpdatedAt = clock.NowLocal;
            await imageRepo.UpdateAsync(image, ct);
            await uow.SaveChangesAsync(ct);
        }
    }
}
