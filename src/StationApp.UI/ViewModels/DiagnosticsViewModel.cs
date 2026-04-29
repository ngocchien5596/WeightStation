using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.Device.Models;

namespace StationApp.UI.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly IWeightFrameParser _parser;

    private int _isUiUpdatePending;
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private int _isWeightUpdatePending;
    private DateTime _lastWeightUpdate = DateTime.MinValue;

    [ObservableProperty] private string _rawFrame = "(cho du lieu...)";
    [ObservableProperty] private string _substringPreview = string.Empty;
    [ObservableProperty] private string _parserType = string.Empty;
    [ObservableProperty] private string? _deviceError;
    [ObservableProperty] private string _deviceConnectionStatus = "Dang kiem tra...";
    [ObservableProperty] private SolidColorBrush _deviceConnectionBrush = new(Colors.Gray);
    [ObservableProperty] private decimal _liveWeight;
    [ObservableProperty] private bool _liveIsStable;

    [ObservableProperty] private int _pendingSyncCount;
    [ObservableProperty] private string? _lastSyncError;
    [ObservableProperty] private string _appVersion = string.Empty;
    [ObservableProperty] private string _dbStatus = "OK";
    [ObservableProperty] private string? _centralApiUrl;
    [ObservableProperty] private string? _lastMasterDataSync;
    [ObservableProperty] private string _masterDataSyncStatus = "Unknown";
    [ObservableProperty] private string? _masterDataSyncError;

    public DiagnosticsViewModel(IServiceScopeFactory scopeFactory, IScaleDevice scaleDevice, IWeightFrameParser parser)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _parser = parser;
    }

    public async Task InitializeAsync()
    {
        _scaleDevice.WeightReceived += OnWeightReceived;

        if (_scaleDevice is SerialScaleDevice serial)
        {
            serial.DiagnosticsReceived += OnDiagnosticsReceived;
            serial.ErrorOccurred += OnDeviceError;
            ParserType = serial.ParserTypeName;
            DeviceError = serial.LastError;
            DeviceConnectionStatus = serial.ConnectionState;
        }
        else
        {
            ParserType = _scaleDevice.GetType().Name;
        }

        UpdateDeviceStatus();
        await LoadSyncInfoAsync();
        LoadAppVersion();
    }

    private void OnWeightReceived(object? sender, ScaleReading reading)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastWeightUpdate).TotalMilliseconds < 250 && reading.IsStable == LiveIsStable)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isWeightUpdatePending, 1, 0) != 0)
        {
            return;
        }

        _lastWeightUpdate = now;
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            try
            {
                LiveWeight = reading.Weight;
                LiveIsStable = reading.IsStable;
                if (string.IsNullOrEmpty(RawFrame) || RawFrame == "(cho du lieu...)")
                {
                    RawFrame = reading.RawPayload;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isWeightUpdatePending, 0);
            }
        });
    }

    private void OnDiagnosticsReceived(object? sender, string raw)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastUiUpdate).TotalMilliseconds < 250)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isUiUpdatePending, 1, 0) != 0)
        {
            return;
        }

        _lastUiUpdate = now;
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            try
            {
                RawFrame = raw.Replace("\r", "\\r").Replace("\n", "\\n");

                if (_parser is YaohuaWeightFrameParser parser &&
                    (parser.WeightSubstringStart.HasValue || parser.WeightSubstringLength.HasValue))
                {
                    var start = parser.WeightSubstringStart ?? 0;
                    var length = parser.WeightSubstringLength ?? raw.Length;

                    if (start >= 0 && start < raw.Length)
                    {
                        if (start + length > raw.Length)
                        {
                            length = raw.Length - start;
                        }

                        SubstringPreview = raw.Substring(start, length).Trim();
                    }
                    else
                    {
                        SubstringPreview = raw.Trim();
                    }
                }
                else
                {
                    SubstringPreview = raw.Trim();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isUiUpdatePending, 0);
            }
        });
    }

    private void OnDeviceError(object? sender, string error)
    {
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            DeviceError = error;
            UpdateDeviceStatus();
        });
    }

    private void UpdateDeviceStatus()
    {
        if (_scaleDevice.IsConnected)
        {
            DeviceConnectionStatus = "Dang hoat dong";
            DeviceConnectionBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
            return;
        }

        if (_scaleDevice is SerialScaleDevice serial && !string.IsNullOrWhiteSpace(serial.ConnectionState))
        {
            DeviceConnectionStatus = serial.ConnectionState;
            DeviceConnectionBrush = serial.ConnectionState switch
            {
                "Connecting" => new SolidColorBrush(Colors.Goldenrod),
                "ReconnectWaiting" => new SolidColorBrush(Colors.Orange),
                "Faulted" => new SolidColorBrush(Colors.DarkRed),
                _ => new SolidColorBrush(Colors.Red)
            };
            return;
        }

        DeviceConnectionStatus = "Mat ket noi";
        DeviceConnectionBrush = new SolidColorBrush(Colors.Red);
    }

    private async Task LoadSyncInfoAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var pending = await outboxRepo.GetPendingAsync(clock.NowLocal, 1000, CancellationToken.None);
        PendingSyncCount = pending.Count;

        CentralApiUrl = await appConfig.GetValueAsync("central_api_url", CancellationToken.None) ?? "(chua cau hinh)";
        LastMasterDataSync = await appConfig.GetValueAsync("master_data_last_sync", CancellationToken.None) ?? "(chua dong bo)";
        MasterDataSyncStatus = await appConfig.GetValueAsync("master_data_sync_status", CancellationToken.None) ?? "Unknown";
        MasterDataSyncError = await appConfig.GetValueAsync("master_data_sync_error", CancellationToken.None);
    }

    private void LoadAppVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        AppVersion = assembly?.GetName().Version?.ToString() ?? "1.0.0";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        UpdateDeviceStatus();
        await LoadSyncInfoAsync();
    }

    public void Dispose()
    {
        _scaleDevice.WeightReceived -= OnWeightReceived;
        if (_scaleDevice is SerialScaleDevice serial)
        {
            serial.DiagnosticsReceived -= OnDiagnosticsReceived;
            serial.ErrorOccurred -= OnDeviceError;
        }
    }
}
