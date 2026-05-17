using System;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.Device.Models;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.UI.Resources;

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

    [ObservableProperty] private string _rawFrame = UiText.Diagnostics.WaitingData;
    [ObservableProperty] private string _substringPreview = string.Empty;
    [ObservableProperty] private string _parserType = string.Empty;
    [ObservableProperty] private string? _deviceError;
    [ObservableProperty] private string _deviceConnectionStatus = UiText.Diagnostics.CheckingConnection;
    [ObservableProperty] private SolidColorBrush _deviceConnectionBrush = new(Colors.Gray);
    [ObservableProperty] private decimal _liveWeight;
    [ObservableProperty] private bool _liveIsStable;

    [ObservableProperty] private int _pendingSyncCount;
    [ObservableProperty] private int _failedSyncCount;
    [ObservableProperty] private string? _lastSyncError;
    [ObservableProperty] private string _lastSyncSuccessAt = "N/A";
    [ObservableProperty] private string _lastSyncFailureAt = "N/A";
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
                if (string.IsNullOrEmpty(RawFrame) || RawFrame == UiText.Diagnostics.WaitingData)
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
            DeviceConnectionStatus = UiText.Diagnostics.ActiveConnection;
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

        DeviceConnectionStatus = UiText.Diagnostics.LostConnection;
        DeviceConnectionBrush = new SolidColorBrush(Colors.Red);
    }

    private async Task LoadSyncInfoAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        PendingSyncCount = await context.SyncOutbox
            .Where(o => o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE)
            .CountAsync(CancellationToken.None);

        FailedSyncCount = await context.SyncOutbox
            .Where(o => o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.FAILED_FINAL)
            .CountAsync(CancellationToken.None);

        var lastFailure = await context.SyncOutbox
            .Where(o => !string.IsNullOrWhiteSpace(o.LastError))
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);

        var lastSuccess = await context.SyncOutbox
            .Where(o => o.Status == OutboxStatus.SUCCESS)
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);

        LastSyncError = lastFailure?.LastError;
        LastSyncFailureAt = FormatTimestamp(lastFailure?.UpdatedAt ?? lastFailure?.CreatedAt);
        LastSyncSuccessAt = FormatTimestamp(lastSuccess?.UpdatedAt ?? lastSuccess?.CreatedAt);

        CentralApiUrl = await appConfig.GetValueAsync("central_api_url", CancellationToken.None) ?? UiText.Diagnostics.CentralApiNotConfigured;

        var lastMasterSuccess = await context.SyncOutbox.AsNoTracking()
            .Where(o =>
                (o.AggregateType == SyncAggregateTypes.Vehicle
                || o.AggregateType == SyncAggregateTypes.Customer
                || o.AggregateType == SyncAggregateTypes.Product)
                && o.Status == OutboxStatus.SUCCESS)
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);
        var lastMasterFailure = await context.SyncOutbox.AsNoTracking()
            .Where(o =>
                (o.AggregateType == SyncAggregateTypes.Vehicle
                || o.AggregateType == SyncAggregateTypes.Customer
                || o.AggregateType == SyncAggregateTypes.Product)
                && !string.IsNullOrWhiteSpace(o.LastError))
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);
        var pendingMasterCount = await context.SyncOutbox.AsNoTracking()
            .Where(o =>
                (o.AggregateType == SyncAggregateTypes.Vehicle
                || o.AggregateType == SyncAggregateTypes.Customer
                || o.AggregateType == SyncAggregateTypes.Product)
                && (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.PROCESSING))
            .CountAsync(CancellationToken.None);

        LastMasterDataSync = lastMasterSuccess == null
            ? UiText.Diagnostics.MasterDataNotSynced
            : FormatTimestamp(lastMasterSuccess.UpdatedAt ?? lastMasterSuccess.CreatedAt);
        MasterDataSyncStatus = pendingMasterCount > 0 ? $"Pending outbound ({pendingMasterCount})" : "No pending";
        MasterDataSyncError = lastMasterFailure?.LastError;
    }

    private void LoadAppVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        AppVersion = assembly?.GetName().Version?.ToString() ?? "1.0.0";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        RawFrame = UiText.Diagnostics.WaitingData;
        SubstringPreview = string.Empty;
        DeviceError = null;
        DeviceConnectionStatus = UiText.Diagnostics.CheckingConnection;
        DeviceConnectionBrush = new SolidColorBrush(Colors.Gray);
        LiveWeight = 0;
        LiveIsStable = false;
        PendingSyncCount = 0;
        FailedSyncCount = 0;
        LastSyncError = null;
        LastSyncSuccessAt = "N/A";
        LastSyncFailureAt = "N/A";
        CentralApiUrl = null;
        LastMasterDataSync = null;
        MasterDataSyncStatus = "Unknown";
        MasterDataSyncError = null;
        UpdateDeviceStatus();
        await LoadSyncInfoAsync();
        LoadAppVersion();
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

    private static string FormatTimestamp(DateTime? timestamp)
    {
        return timestamp?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
    }
}
