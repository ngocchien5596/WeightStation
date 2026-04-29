using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Infrastructure.Persistence;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings
{
    public partial class SyncInfoViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SyncInfoViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty] private int _pendingRegistrationsCount = 0;
        [ObservableProperty] private int _pendingTicketsCount = 0;
        [ObservableProperty] private int _unprocessedInboundCount = 0;
        [ObservableProperty] private string _lastSyncError = "N/A";

        public async Task LoadAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            try
            {
                PendingRegistrationsCount = await context.SyncOutbox
                    .Where(o => o.AggregateType == "VehicleRegistration" && (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE))
                    .CountAsync(CancellationToken.None);

                PendingTicketsCount = await context.SyncOutbox
                    .Where(o => o.AggregateType == "WeighTicket" && (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE))
                    .CountAsync(CancellationToken.None);

                UnprocessedInboundCount = await context.VehicleRegistrations
                    .Where(r => !r.IsInboundProcessed)
                    .CountAsync(CancellationToken.None);

                var lastFailItem = await context.SyncOutbox
                    .Where(o => o.LastError != null)
                    .OrderByDescending(o => o.UpdatedAt)
                    .FirstOrDefaultAsync(CancellationToken.None);

                LastSyncError = lastFailItem?.LastError ?? "Không có lỗi ghi nhận.";
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi truy xuất trạng thái đồng bộ: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
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
    }
}
