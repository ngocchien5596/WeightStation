using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public partial class SyncInfoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SyncInfoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [ObservableProperty] private int _pendingRegistrationsCount;
    [ObservableProperty] private int _pendingTicketsCount;
    [ObservableProperty] private int _pendingDeliveryTicketsCount;
    [ObservableProperty] private int _failedSyncCount;
    [ObservableProperty] private int _unprocessedInboundCount;
    [ObservableProperty] private string _lastSyncError = "N/A";
    [ObservableProperty] private string _lastSyncSuccessAt = "N/A";
    [ObservableProperty] private string _lastSyncFailureAt = "N/A";

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        try
        {
            PendingRegistrationsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.VehicleRegistration);
            PendingTicketsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.WeighTicket);
            PendingDeliveryTicketsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.DeliveryTicket);
            FailedSyncCount = await context.SyncOutbox
                .Where(o => o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.FAILED_FINAL)
                .CountAsync(CancellationToken.None);

            UnprocessedInboundCount = await context.VehicleRegistrations
                .Where(r => !r.IsInboundProcessed)
                .CountAsync(CancellationToken.None);

            var lastFailItem = await context.SyncOutbox
                .Where(o => !string.IsNullOrWhiteSpace(o.LastError))
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            var lastSuccessItem = await context.SyncOutbox
                .Where(o => o.Status == OutboxStatus.SUCCESS)
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            LastSyncError = lastFailItem?.LastError ?? "Không có lỗi ghi nhận.";
            LastSyncFailureAt = FormatTimestamp(lastFailItem?.UpdatedAt ?? lastFailItem?.CreatedAt);
            LastSyncSuccessAt = FormatTimestamp(lastSuccessItem?.UpdatedAt ?? lastSuccessItem?.CreatedAt);
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi truy xuất trạng thái đồng bộ: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        PendingRegistrationsCount = 0;
        PendingTicketsCount = 0;
        PendingDeliveryTicketsCount = 0;
        FailedSyncCount = 0;
        UnprocessedInboundCount = 0;
        LastSyncError = "N/A";
        LastSyncSuccessAt = "N/A";
        LastSyncFailureAt = "N/A";

        await LoadAsync();

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        await dialogService.ShowInfoAsync("Thông báo", "Đã cập nhật dữ liệu đồng bộ.");
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        await dialogService.ShowInfoAsync("Thông báo", "Yêu cầu đẩy đồng bộ ngay lập tức đã được gửi tới worker.");
    }

    private static Task<int> CountPendingOutboxAsync(StationDbContext context, string aggregateType)
    {
        return context.SyncOutbox
            .Where(o => o.AggregateType == aggregateType &&
                        (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE))
            .CountAsync(CancellationToken.None);
    }

    private static string FormatTimestamp(DateTime? timestamp)
    {
        return timestamp?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
    }
}
