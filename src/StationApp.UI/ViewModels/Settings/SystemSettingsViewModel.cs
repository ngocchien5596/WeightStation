using System.Globalization;
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
    [ObservableProperty] private string _toleranceKgPerBag = AppConfigDefaults.DefaultToleranceKgPerBag.ToString("0.##", CultureInfo.InvariantCulture);
    [ObservableProperty] private string _syncIntervalSeconds = AppConfigDefaults.DefaultSyncIntervalSeconds;
    [ObservableProperty] private string _registrationInboundPollSeconds = AppConfigDefaults.DefaultRegistrationInboundPollSeconds;
    [ObservableProperty] private string _overweightSplitStepWeight = AppConfigDefaults.DefaultOverweightSplitStepWeight.ToString("0.####", CultureInfo.InvariantCulture);
    [ObservableProperty] private string _centralApiUrl = string.Empty;
    [ObservableProperty] private string _centralApiKey = string.Empty;
    [ObservableProperty] private string _localDatabaseBackupDirectory = string.Empty;
    [ObservableProperty] private string _localDatabaseBackupTime = AppConfigDefaults.DefaultLocalDatabaseBackupTime;
    [ObservableProperty] private string _centralApiHealthMessage = "Chưa kiểm tra kết nối Central API.";
    [ObservableProperty] private string _localDatabaseBackupMessage = "Chưa chạy sao lưu DB local thủ công.";

    public bool CanManageSystemSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        StationCode = await repo.GetValueAsync("station_code", CancellationToken.None) ?? string.Empty;
        TicketPrefix = await repo.GetValueAsync("ticket_prefix", CancellationToken.None) ?? string.Empty;
        DeliveryPrefix = await repo.GetValueAsync("delivery_prefix", CancellationToken.None) ?? "DN";
        ToleranceKgPerBag = await repo.GetValueAsync(AppConfigKeys.ToleranceKgPerBag, CancellationToken.None)
            ?? AppConfigDefaults.DefaultToleranceKgPerBag.ToString("0.##", CultureInfo.InvariantCulture);
        SyncIntervalSeconds = await repo.GetValueAsync(AppConfigKeys.SyncIntervalSeconds, CancellationToken.None)
            ?? await repo.GetValueAsync("sync_interval", CancellationToken.None)
            ?? AppConfigDefaults.DefaultSyncIntervalSeconds;
        RegistrationInboundPollSeconds = await repo.GetValueAsync(AppConfigKeys.RegistrationInboundPollSeconds, CancellationToken.None)
            ?? AppConfigDefaults.DefaultRegistrationInboundPollSeconds;
        OverweightSplitStepWeight = await repo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, CancellationToken.None)
            ?? AppConfigDefaults.DefaultOverweightSplitStepWeight.ToString("0.####", CultureInfo.InvariantCulture);
        CentralApiUrl = await repo.GetValueAsync(AppConfigKeys.CentralApiUrl, CancellationToken.None) ?? string.Empty;
        CentralApiKey = await repo.GetValueAsync(AppConfigKeys.CentralApiKey, CancellationToken.None) ?? string.Empty;
        LocalDatabaseBackupDirectory = await repo.GetValueAsync(AppConfigKeys.LocalDatabaseBackupDirectory, CancellationToken.None)
            ?? AppConfigDefaults.DefaultLocalDatabaseBackupDirectory;
        LocalDatabaseBackupTime = await repo.GetValueAsync(AppConfigKeys.LocalDatabaseBackupTime, CancellationToken.None)
            ?? AppConfigDefaults.DefaultLocalDatabaseBackupTime;
        CentralApiHealthMessage = "Chưa kiểm tra kết nối Central API.";
        LocalDatabaseBackupMessage = $"Thư mục backup hiện tại: {LocalDatabaseBackupDirectory}";
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
                    OverweightSplitStepWeight,
                    CentralApiUrl,
                    CentralApiKey,
                    LocalDatabaseBackupDirectory,
                    LocalDatabaseBackupTime),
                CancellationToken.None);
            await dialogService.ShowInfoAsync("Thông báo", "Đã lưu tham số hệ thống thành công.");
        }
        catch (InvalidOperationException ex)
        {
            await dialogService.ShowWarningAsync("Lỗi", ex.Message);
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSystemSettings))]
    private async Task TestCentralApiConnectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();
        var checker = scope.ServiceProvider.GetRequiredService<ICentralApiHealthChecker>();
        var result = await checker.CheckAsync(CentralApiUrl, CentralApiKey, CancellationToken.None);
        CentralApiHealthMessage = result.Message;

        if (result.Success)
        {
            await dialogService.ShowInfoAsync("Thông báo", result.Message);
            return;
        }

        await dialogService.ShowWarningAsync("Cảnh báo", result.Message);
    }

    [RelayCommand(CanExecute = nameof(CanManageSystemSettings))]
    private async Task RunLocalDatabaseBackupNowAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();
        var backupService = scope.ServiceProvider.GetRequiredService<Services.ILocalDatabaseBackupService>();
        var result = await backupService.RunBackupNowAsync(CancellationToken.None);
        LocalDatabaseBackupMessage = result.Message;

        if (result.Success)
        {
            await dialogService.ShowInfoAsync("Thông báo", result.Message);
            return;
        }

        await dialogService.ShowWarningAsync("Cảnh báo", result.Message);
    }
}
