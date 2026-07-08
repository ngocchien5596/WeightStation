using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.UI.Helpers;
using StationApp.UI.Resources;

namespace StationApp.UI.Services;

public sealed class WeighingDeviceConnector : IDisposable
{
    private readonly IWeighingDeviceHost _host;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly ILogger? _logger;
    private readonly Dispatcher _uiDispatcher;

    private readonly DispatcherTimer _scaleUiTimer;
    private readonly object _scaleReadingLock = new();
    private LatestScaleReadingSnapshot? _pendingScaleReading;
    private bool _pendingScaleDeviceConnected;
    private bool _hasStartedDeviceAttach;

    private CameraSystemSettings? _cameraSettings;
    private Guid? _currentPreviewSessionId;
    private long _lastRenderedPreviewSequence;
    private CameraPreviewFrameReceivedEventArgs? _latestPendingPreviewFrame;
    private int _isPreviewUiUpdatePending;

    private static readonly SolidColorBrush StableBrush = new(Color.FromRgb(46, 213, 115));
    private static readonly SolidColorBrush UnstableBrush = new(Colors.Orange);

    public WeighingDeviceConnector(
        IWeighingDeviceHost host,
        IScaleDevice scaleDevice,
        ICameraPreviewService cameraPreviewService,
        ILogger? logger = null)
    {
        _host = host;
        _scaleDevice = scaleDevice;
        _cameraPreviewService = cameraPreviewService;
        _logger = logger;
        _uiDispatcher = Dispatcher.CurrentDispatcher;

        _scaleUiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _scaleUiTimer.Tick += OnScaleUiTimerTick;

        _scaleDevice.WeightReceived += OnWeightReceived;
        _cameraPreviewService.StatusChanged += OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived += OnCameraPreviewFrameReceived;
    }

    public void StartDeviceAttachIfNeeded()
    {
        if (_hasStartedDeviceAttach)
        {
            return;
        }

        _hasStartedDeviceAttach = true;
        _scaleUiTimer.Start();
        _ = Task.Run(async () =>
        {
            try
            {
                if (!_scaleDevice.IsConnected)
                {
                    await _scaleDevice.ConnectAsync(CancellationToken.None);
                    await _scaleDevice.StartAsync(CancellationToken.None);
                }
                
                var isConnected = _scaleDevice.IsConnected;
                _ = _uiDispatcher.BeginInvoke(() =>
                {
                    _host.IsDeviceConnected = isConnected;
                    _host.DeviceStatusText = isConnected ? UiText.Weighing.ActiveConnection : UiText.Weighing.LostConnection;
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Background device attach failed");
                _ = _uiDispatcher.BeginInvoke(() =>
                {
                    _host.DeviceStatusText = UiText.Weighing.LostConnection;
                    _host.IsDeviceConnected = false;
                });
            }
        });
    }

    private void OnWeightReceived(object? sender, ScaleReading reading)
    {
        lock (_scaleReadingLock)
        {
            _pendingScaleReading = new LatestScaleReadingSnapshot
            {
                Weight = reading.Weight,
                IsStable = reading.IsStable,
                ReceivedAt = reading.CapturedAt
            };
            _pendingScaleDeviceConnected = _scaleDevice.IsConnected;
        }

        if (!_scaleUiTimer.IsEnabled)
        {
            _scaleUiTimer.Start();
        }
    }

    private void OnScaleUiTimerTick(object? sender, EventArgs e)
    {
        LatestScaleReadingSnapshot? latestReading;
        bool deviceConnected;

        lock (_scaleReadingLock)
        {
            latestReading = _pendingScaleReading;
            deviceConnected = _pendingScaleDeviceConnected;
            _pendingScaleReading = null;
        }

        if (latestReading == null)
        {
            return;
        }

        if (_host.IsAutoMode)
        {
            _host.CurrentWeight = latestReading.Weight;
            _host.IsStable = latestReading.IsStable;
            _host.StabilityText = latestReading.IsStable ? "ỔN ĐỊNH" : "CHƯA ỔN ĐỊNH";
            _host.StabilityBrush = latestReading.IsStable ? StableBrush : UnstableBrush;
        }
        else
        {
            _host.IsStable = true;
            _host.StabilityText = "CÂN TAY";
            _host.StabilityBrush = StableBrush;
        }

        _host.IsDeviceConnected = deviceConnected;
        _host.DeviceStatusText = deviceConnected ? UiText.Weighing.ActiveConnection : UiText.Weighing.LostConnection;
    }

    public void InitializeCameraPreview(CameraSystemSettings settings)
    {
        _cameraSettings = settings;
        _host.Camera1PreviewName = settings.Camera1.DisplayName;
        _host.Camera2PreviewName = settings.Camera2.DisplayName;
        _host.IsCamera1PreviewAvailable = settings.Camera1.IsEnabled && !string.IsNullOrWhiteSpace(settings.Camera1.EffectivePreviewRtspUrl);
        _host.IsCamera2PreviewAvailable = settings.Camera2.IsEnabled && !string.IsNullOrWhiteSpace(settings.Camera2.EffectivePreviewRtspUrl);
        _host.IsCameraPreviewAvailable = _host.IsCamera1PreviewAvailable || _host.IsCamera2PreviewAvailable;

        var preferred = string.IsNullOrWhiteSpace(settings.PreviewDefaultCameraCode)
            ? "CAM1"
            : settings.PreviewDefaultCameraCode.Trim().ToUpperInvariant();

        var targetCameraCode =
            preferred == "CAM2" && _host.IsCamera2PreviewAvailable ? "CAM2" :
            _host.IsCamera1PreviewAvailable ? "CAM1" :
            _host.IsCamera2PreviewAvailable ? "CAM2" :
            preferred;

        _host.RaisePropertyChanged(nameof(IWeighingDeviceHost.IsCamera1PreviewAvailable));
        _host.RaisePropertyChanged(nameof(IWeighingDeviceHost.IsCamera2PreviewAvailable));
        _host.RaisePropertyChanged("ShowCamera1Selector");
        _host.RaisePropertyChanged("ShowCamera2Selector");
        _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");

        if (!string.Equals(_host.SelectedPreviewCameraCode, targetCameraCode, StringComparison.OrdinalIgnoreCase))
        {
            _host.SelectedPreviewCameraCode = targetCameraCode;
        }
        else if (!_host.IsCameraPreviewAvailable)
        {
            _host.CameraPreviewStatusText = "Chưa cấu hình camera";
            _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
        }
    }

    public async Task StartCameraPreviewAsync(string cameraCode)
    {
        if (_cameraSettings == null)
        {
            return;
        }

        var camera = ResolvePreviewCamera(cameraCode);
        if (camera == null)
        {
            _host.CameraPreviewStatusText = _host.IsCameraPreviewAvailable ? "Camera chưa sẵn sàng" : "Chưa cấu hình camera";
            ResetPreviewRenderState();
            _ = _cameraPreviewService.StopPreviewAsync();
            _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
            return;
        }

        ResetPreviewRenderState();
        _host.CameraPreviewStatusText = "Đang kết nối";
        try
        {
            await _cameraPreviewService.StartPreviewAsync(camera, CancellationToken.None);
            _currentPreviewSessionId = _cameraPreviewService.ActivePreviewSessionId;
            _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Start preview for camera {CameraCode} failed", camera.CameraCode);
            ResetPreviewRenderState();
            _host.CameraPreviewStatusText = "Không kết nối được camera";
            _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
        }
    }

    private CameraEndpointSettings? ResolvePreviewCamera(string? cameraCode)
    {
        if (_cameraSettings == null || string.IsNullOrWhiteSpace(cameraCode))
        {
            return null;
        }

        return cameraCode.Trim().ToUpperInvariant() switch
        {
            "CAM1" when _host.IsCamera1PreviewAvailable => _cameraSettings.Camera1,
            "CAM2" when _host.IsCamera2PreviewAvailable => _cameraSettings.Camera2,
            _ => null
        };
    }

    private void OnCameraPreviewStatusChanged(object? sender, CameraPreviewStatusChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.CameraCode)
            && !string.Equals(e.CameraCode, _host.SelectedPreviewCameraCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = _uiDispatcher.BeginInvoke(() =>
        {
            _host.CameraPreviewStatusText = e.StatusText;
            _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
        });
    }

    private void OnCameraPreviewFrameReceived(object? sender, CameraPreviewFrameReceivedEventArgs e)
    {
        if (e.PreviewSessionId != _currentPreviewSessionId)
        {
            return;
        }

        if (e.Sequence <= Interlocked.Read(ref _lastRenderedPreviewSequence))
        {
            return;
        }

        _latestPendingPreviewFrame = e;
        if (Interlocked.Exchange(ref _isPreviewUiUpdatePending, 1) == 1)
        {
            return;
        }

        _ = _uiDispatcher.BeginInvoke(() =>
        {
            try
            {
                var latest = _latestPendingPreviewFrame;
                if (latest == null)
                {
                    return;
                }

                if (latest.PreviewSessionId != _currentPreviewSessionId)
                {
                    return;
                }

                if (latest.Sequence <= Interlocked.Read(ref _lastRenderedPreviewSequence))
                {
                    return;
                }

                _host.CameraPreviewSource = latest.Frame;
                _host.RaisePropertyChanged("ShowCameraPreviewPlaceholder");
                Interlocked.Exchange(ref _lastRenderedPreviewSequence, latest.Sequence);
            }
            finally
            {
                Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
                if (_latestPendingPreviewFrame != null && _latestPendingPreviewFrame.Sequence > Interlocked.Read(ref _lastRenderedPreviewSequence))
                {
                    OnCameraPreviewFrameReceived(this, _latestPendingPreviewFrame);
                }
            }
        }, DispatcherPriority.Render);
    }

    public void ResetPreviewRenderState()
    {
        _currentPreviewSessionId = null;
        _latestPendingPreviewFrame = null;
        Interlocked.Exchange(ref _lastRenderedPreviewSequence, 0);
        Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
        _host.CameraPreviewSource = null;
    }

    public void Dispose()
    {
        _scaleUiTimer.Stop();
        _scaleUiTimer.Tick -= OnScaleUiTimerTick;
        _scaleDevice.WeightReceived -= OnWeightReceived;
        _cameraPreviewService.StatusChanged -= OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived -= OnCameraPreviewFrameReceived;
        ResetPreviewRenderState();
        try
        {
            _ = _cameraPreviewService.StopPreviewAsync();
        }
        catch
        {
            // ignore preview stop failures during dispose
        }
    }
}
