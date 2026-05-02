using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings
{
    public partial class SystemSettingsViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SystemSettingsViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty] private string _stationCode = string.Empty;
        [ObservableProperty] private string _ticketPrefix = string.Empty;
        [ObservableProperty] private string _deliveryPrefix = string.Empty;
        [ObservableProperty] private string _toleranceKg = "0";
        [ObservableProperty] private string _syncIntervalSeconds = "30";
        [ObservableProperty] private string _registrationInboundPollSeconds = "15";
        [ObservableProperty] private string _overweightSplitStepWeight = "0.0025";
        [ObservableProperty] private bool _pilotModeEnabled;

        public async Task LoadAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

            StationCode = await repo.GetValueAsync("station_code", CancellationToken.None) ?? string.Empty;
            TicketPrefix = await repo.GetValueAsync("ticket_prefix", CancellationToken.None) ?? string.Empty;
            DeliveryPrefix = await repo.GetValueAsync("delivery_prefix", CancellationToken.None) ?? "DN";
            ToleranceKg = await repo.GetValueAsync("tolerance_kg", CancellationToken.None) ?? "0";
            SyncIntervalSeconds = await repo.GetValueAsync("sync_interval", CancellationToken.None) ?? "30";
            RegistrationInboundPollSeconds = await repo.GetValueAsync("registration_inbound_poll_seconds", CancellationToken.None) ?? "15";
            OverweightSplitStepWeight = await repo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, CancellationToken.None)
                ?? AppConfigDefaults.DefaultOverweightSplitStepWeight.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            
            var pilotStr = await repo.GetValueAsync("pilot_mode_enabled", CancellationToken.None);
            PilotModeEnabled = pilotStr == "true";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            try
            {
                await repo.SetValueAsync("station_code", StationCode.Trim(), CancellationToken.None);
                await repo.SetValueAsync("ticket_prefix", TicketPrefix.Trim(), CancellationToken.None);
                await repo.SetValueAsync("delivery_prefix", DeliveryPrefix.Trim(), CancellationToken.None);
                await repo.SetValueAsync("tolerance_kg", ToleranceKg.Trim(), CancellationToken.None);
                await repo.SetValueAsync("sync_interval", SyncIntervalSeconds.Trim(), CancellationToken.None);
                await repo.SetValueAsync("registration_inbound_poll_seconds", RegistrationInboundPollSeconds.Trim(), CancellationToken.None);
                await repo.SetValueAsync(AppConfigKeys.OverweightSplitStepWeight, OverweightSplitStepWeight.Trim(), CancellationToken.None);
                await repo.SetValueAsync("pilot_mode_enabled", PilotModeEnabled ? "true" : "false", CancellationToken.None);

                await uow.SaveChangesAsync(CancellationToken.None);
                await dialogService.ShowInfoAsync("Thông báo", "Lưu tham số hệ thống thành công!");
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình: {ex.Message}");
            }
        }
    }
}
