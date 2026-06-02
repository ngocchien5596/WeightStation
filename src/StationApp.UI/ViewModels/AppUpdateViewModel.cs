using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class AppUpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _appUpdateService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDialogService _dialogService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;

    private AppUpdateManifest? _lastManifest;

    public AppUpdateViewModel(
        IAppUpdateService appUpdateService,
        ICurrentUserContext currentUserContext,
        IDialogService dialogService,
        IServiceScopeFactory scopeFactory,
        IToastService toastService)
    {
        _appUpdateService = appUpdateService;
        _currentUserContext = currentUserContext;
        _dialogService = dialogService;
        _scopeFactory = scopeFactory;
        _toastService = toastService;
    }

    [ObservableProperty] private string _currentVersion = string.Empty;
    [ObservableProperty] private string _latestVersion = "\u0043\u0068\u01B0\u0061\u0020\u006B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061";
    [ObservableProperty] private string _releaseNotes = "\u004E\u0068\u1EA5\u006E\u0020\u0027\u004B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0027\u0020\u0111\u1EC3\u0020\u0111\u1ECD\u0063\u0020\u0074\u0068\u00F4\u006E\u0067\u0020\u0074\u0069\u006E\u0020\u0062\u1EA3\u006E\u0020\u006D\u1EDB\u0069\u002E";
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "\u0053\u1EB5\u006E\u0020\u0073\u00E0\u006E\u0067\u0020\u006B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E";
    [ObservableProperty] private string _sharedReleaseRoot = string.Empty;
    [ObservableProperty] private string _resolvedManifestPath = string.Empty;

    public bool CanUpdateApplication => StationAuthorization.IsAdmin(_currentUserContext.RoleCode) || StationAuthorization.IsOperator(_currentUserContext.RoleCode);

    public async Task LoadAsync()
    {
        CurrentVersion = _appUpdateService.GetCurrentVersion();
        await LoadConfigurationAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUpdateApplication))]
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CurrentVersion = _appUpdateService.GetCurrentVersion();
            StatusText = "\u0110\u0061\u006E\u0067\u0020\u006B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061\u0020\u0062\u1EA3\u006E\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E\u002E\u002E";

            var result = await _appUpdateService.CheckForUpdatesAsync(CancellationToken.None);
            if (!result.Success)
            {
                _lastManifest = result.Manifest;
                IsUpdateAvailable = false;
                LatestVersion = result.Manifest?.Version ?? "\u004B\u0068\u00F4\u006E\u0067\u0020\u0111\u1ECD\u0063\u0020\u0111\u01B0\u1EE3\u0063";
                ReleaseNotes = result.ErrorMessage ?? "\u004B\u0068\u00F4\u006E\u0067\u0020\u0074\u0068\u1EC3\u0020\u006B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E";
                StatusText = "\u004B\u0069\u1EC3\u006D\u0020\u0074\u0072\u0061\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u0074\u0068\u1EA5\u0074\u0020\u0062\u1EA1\u0069\u002E";
                await _dialogService.ShowWarningAsync("\u0043\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u1EE9\u006E\u0067\u0020\u0064\u1EE5\u006E\u0067", ReleaseNotes);
                return;
            }

            _lastManifest = result.Manifest;
            LatestVersion = result.Manifest?.Version ?? "\u004B\u0068\u00F4\u006E\u0067\u0020\u0063\u00F3";
            ReleaseNotes = string.IsNullOrWhiteSpace(result.Manifest?.ReleaseNotes)
                ? "\u004B\u0068\u00F4\u006E\u0067\u0020\u0063\u00F3\u0020\u0067\u0068\u0069\u0020\u0063\u0068\u00FA\u0020\u0070\u0068\u00E1\u0074\u0020\u0068\u00E0\u006E\u0068\u002E"
                : result.Manifest!.ReleaseNotes!;
            IsUpdateAvailable = result.IsUpdateAvailable;

            if (result.IsUpdateAvailable)
            {
                StatusText = $"\u0110\u00E3\u0020\u0074\u00EC\u006D\u0020\u0074\u0068\u1EA5\u0079\u0020\u0062\u1EA3\u006E\u0020\u006D\u1EDB\u0069\u0020{LatestVersion}.";
                _toastService.ShowInfo($"\u0110\u00E3\u0020\u0074\u00EC\u006D\u0020\u0074\u0068\u1EA5\u0079\u0020\u0062\u1EA3\u006E\u0020\u006D\u1EDB\u0069\u0020{LatestVersion}.");
            }
            else
            {
                StatusText = "\u0110\u0061\u006E\u0067\u0020\u006C\u00E0\u0020\u0070\u0068\u0069\u00EA\u006E\u0020\u0062\u1EA3\u006E\u0020\u006D\u1EDB\u0069\u0020\u006E\u0068\u1EA5\u0074\u002E";
                _toastService.ShowInfo("\u0110\u0061\u006E\u0067\u0020\u006C\u00E0\u0020\u0070\u0068\u0069\u00EA\u006E\u0020\u0062\u1EA3\u006E\u0020\u006D\u1EDB\u0069\u0020\u006E\u0068\u1EA5\u0074\u002E");
            }
        }
        finally
        {
            IsBusy = false;
            UpdateCommand.NotifyCanExecuteChanged();
            SaveConfigurationCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private async Task UpdateAsync()
    {
        if (_lastManifest == null)
        {
            await CheckForUpdatesAsync();
        }

        if (_lastManifest == null || !IsUpdateAvailable)
        {
            _toastService.ShowInfo("\u004B\u0068\u00F4\u006E\u0067\u0020\u0063\u00F3\u0020\u0062\u1EA3\u006E\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u006D\u1EDB\u0069\u0020\u0111\u1EC3\u0020\u0063\u00E0\u0069\u0020\u0111\u1EB7\u0074\u002E");
            return;
        }

        var confirmMessage =
            $"\u0042\u1EA1\u006E\u0020\u0063\u00F3\u0020\u0063\u0068\u1EAF\u0063\u0020\u0063\u0068\u1EAF\u006E\u0020\u006D\u0075\u1ED1\u006E\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u0074\u1EEB\u0020\u0070\u0068\u0069\u00EA\u006E\u0020\u0062\u1EA3\u006E\u0020{CurrentVersion}\u0020\u006C\u00EA\u006E\u0020{LatestVersion}\u0020\u006B\u0068\u00F4\u006E\u0067\u003F{Environment.NewLine}{Environment.NewLine}" +
            $"\u0047\u0068\u0069\u0020\u0063\u0068\u00FA\u003A{Environment.NewLine}{ReleaseNotes}";

        var confirmed = await _dialogService.ShowConfirmAsync("\u0043\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u1EE9\u006E\u0067\u0020\u0064\u1EE5\u006E\u0067", confirmMessage, "\u0043\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074", "\u0048\u1EE7\u0079");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusText = "\u0110\u0061\u006E\u0067\u0020\u0063\u0068\u0075\u1EA9\u006E\u0020\u0062\u1ECB\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E\u002E\u002E";
            var result = await _appUpdateService.StartUpdateAsync(_lastManifest, CancellationToken.None);
            if (!result.Success)
            {
                StatusText = "\u004B\u0068\u00F4\u006E\u0067\u0020\u0074\u0068\u1EC3\u0020\u0062\u1EAF\u0074\u0020\u0111\u1EA7\u0075\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E";
                await _dialogService.ShowErrorAsync("\u0043\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u1EE9\u006E\u0067\u0020\u0064\u1EE5\u006E\u0067", result.ErrorMessage ?? "\u004B\u0068\u00F4\u006E\u0067\u0020\u0074\u0068\u1EC3\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u0020\u1EE9\u006E\u0067\u0020\u0064\u1EE5\u006E\u0067\u002E");
                return;
            }

            _toastService.ShowInfo("\u0110\u0061\u006E\u0067\u0020\u0111\u00F3\u006E\u0067\u0020\u1EE9\u006E\u0067\u0020\u0064\u1EE5\u006E\u0067\u0020\u0111\u1EC3\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074\u002E\u002E\u002E");
            System.Windows.Application.Current.Shutdown();
        }
        finally
        {
            IsBusy = false;
            UpdateCommand.NotifyCanExecuteChanged();
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
            SaveConfigurationCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStartUpdate() => CanUpdateApplication && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSaveConfiguration))]
    private async Task SaveConfigurationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var normalizedRoot = SharedReleaseRoot?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedRoot))
        {
            await _dialogService.ShowWarningAsync("\u0043\u1EA5\u0075\u0020\u0068\u00EC\u006E\u0068\u0020\u0063\u1EAD\u0070\u0020\u006E\u0068\u1EAD\u0074", "\u0056\u0075\u0069\u0020\u006C\u00F2\u006E\u0067\u0020\u006E\u0068\u1EAD\u0070\u0020\u0111\u01B0\u1EDD\u006E\u0067\u0020\u0064\u1EAB\u006E\u0020\u0073\u0068\u0061\u0072\u0065\u0064\u0020\u0066\u006F\u006C\u0064\u0065\u0072\u002E");
            return;
        }

        IsBusy = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.SetValueAsync(AppConfigKeys.AppUpdateSharedReleaseRoot, normalizedRoot, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            SharedReleaseRoot = normalizedRoot;
            ResolvedManifestPath = BuildManifestPathPreview(normalizedRoot);
            _toastService.ShowSuccess("\u0110\u00E3\u0020\u006C\u01B0\u0075\u0020\u0063\u1EA5\u0075\u0020\u0068\u00EC\u006E\u0068\u0020\u0073\u0068\u0061\u0072\u0065\u0064\u0020\u0066\u006F\u006C\u0064\u0065\u0072\u002E");
        }
        finally
        {
            IsBusy = false;
            SaveConfigurationCommand.NotifyCanExecuteChanged();
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
            UpdateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSaveConfiguration() => CanUpdateApplication && !IsBusy;

    private async Task LoadConfigurationAsync()
    {
        if (_appUpdateService is SharedFolderAppUpdateService sharedFolderService)
        {
            var configuredRoot = await sharedFolderService.GetConfiguredSharedReleaseRootAsync(CancellationToken.None);
            SharedReleaseRoot = configuredRoot;
            ResolvedManifestPath = await sharedFolderService.GetResolvedManifestPathAsync(CancellationToken.None);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var appConfigValue = (await repo.GetValueAsync(AppConfigKeys.AppUpdateSharedReleaseRoot, CancellationToken.None))?.Trim();
        SharedReleaseRoot = appConfigValue ?? string.Empty;
        ResolvedManifestPath = string.IsNullOrWhiteSpace(appConfigValue)
            ? string.Empty
            : BuildManifestPathPreview(appConfigValue);
    }

    private static string BuildManifestPathPreview(string sharedReleaseRoot)
    {
        var trimmed = sharedReleaseRoot.Trim();
        if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return System.IO.Path.Combine(trimmed, "latest.json");
    }
}
