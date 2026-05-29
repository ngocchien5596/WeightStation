using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;

namespace StationApp.UI.ViewModels.Settings;

public partial class SystemSettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;

    public SystemSettingsViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
    }

    [ObservableProperty] private string _stationCode = string.Empty;
    [ObservableProperty] private string _ticketPrefix = string.Empty;
    [ObservableProperty] private string _deliveryPrefix = string.Empty;
    [ObservableProperty] private string _toleranceKgPerBag = AppConfigDefaults.DefaultToleranceKgPerBag.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    [ObservableProperty] private string _syncIntervalSeconds = "30";
    [ObservableProperty] private string _registrationInboundPollSeconds = "15";
    [ObservableProperty] private string _overweightSplitStepWeight = "0.0025";

    public bool CanManageSystemSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        StationCode = await repo.GetValueAsync("station_code", CancellationToken.None) ?? string.Empty;
        TicketPrefix = await repo.GetValueAsync("ticket_prefix", CancellationToken.None) ?? string.Empty;
        DeliveryPrefix = await repo.GetValueAsync("delivery_prefix", CancellationToken.None) ?? "DN";
        ToleranceKgPerBag = await repo.GetValueAsync(AppConfigKeys.ToleranceKgPerBag, CancellationToken.None)
            ?? AppConfigDefaults.DefaultToleranceKgPerBag.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        SyncIntervalSeconds = await repo.GetValueAsync("sync_interval", CancellationToken.None) ?? "30";
        RegistrationInboundPollSeconds = await repo.GetValueAsync("registration_inbound_poll_seconds", CancellationToken.None) ?? "15";
        OverweightSplitStepWeight = await repo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, CancellationToken.None)
            ?? AppConfigDefaults.DefaultOverweightSplitStepWeight.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    [RelayCommand(CanExecute = nameof(CanManageSystemSettings))]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        try
        {
            var useCase = scope.ServiceProvider.GetRequiredService<UpdateSystemSettingsUseCase>();
            await useCase.ExecuteAsync(
                new UpdateSystemSettingsRequest(
                    StationCode,
                    TicketPrefix,
                    DeliveryPrefix,
                    ToleranceKgPerBag,
                    SyncIntervalSeconds,
                    RegistrationInboundPollSeconds,
                    OverweightSplitStepWeight),
                CancellationToken.None);
            await dialogService.ShowInfoAsync("Thông báo", "Lưu tham số hệ thống thành công!");
        }
        catch (InvalidOperationException ex)
        {
            await dialogService.ShowWarningAsync("Loi", ex.Message);
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình: {ex.Message}");
        }
    }
}
