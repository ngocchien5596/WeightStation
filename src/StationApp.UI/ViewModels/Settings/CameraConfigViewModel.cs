using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;

namespace StationApp.UI.ViewModels.Settings;

public partial class CameraConfigViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;
    private bool _isUpdatingDefaultPreviewSelection;

    public CameraConfigViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
    }

    // C2 Cameras
    [ObservableProperty] private bool _camera1Enabled;
    [ObservableProperty] private string _camera1Name = AppConfigDefaults.DefaultCamera1Name;
    [ObservableProperty] private string _camera1RtspUrl = string.Empty;
    [ObservableProperty] private string _camera1PreviewRtspUrl = string.Empty;

    [ObservableProperty] private bool _camera2Enabled;
    [ObservableProperty] private string _camera2Name = AppConfigDefaults.DefaultCamera2Name;
    [ObservableProperty] private string _camera2RtspUrl = string.Empty;
    [ObservableProperty] private string _camera2PreviewRtspUrl = string.Empty;

    // C6 Cameras
    [ObservableProperty] private bool _cameraC6_1Enabled;
    [ObservableProperty] private string _cameraC6_1Name = AppConfigDefaults.DefaultCameraC6_1Name;
    [ObservableProperty] private string _cameraC6_1RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraC6_1PreviewRtspUrl = string.Empty;

    [ObservableProperty] private bool _cameraC6_2Enabled;
    [ObservableProperty] private string _cameraC6_2Name = AppConfigDefaults.DefaultCameraC6_2Name;
    [ObservableProperty] private string _cameraC6_2RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraC6_2PreviewRtspUrl = string.Empty;

    // Crusher cameras
    [ObservableProperty] private bool _cameraCrusher_1Enabled;
    [ObservableProperty] private string _cameraCrusher_1Name = AppConfigDefaults.DefaultCameraCrusher_1Name;
    [ObservableProperty] private string _cameraCrusher_1RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraCrusher_1PreviewRtspUrl = string.Empty;

    [ObservableProperty] private bool _cameraCrusher_2Enabled;
    [ObservableProperty] private string _cameraCrusher_2Name = AppConfigDefaults.DefaultCameraCrusher_2Name;
    [ObservableProperty] private string _cameraCrusher_2RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraCrusher_2PreviewRtspUrl = string.Empty;

    // Clay cameras
    [ObservableProperty] private bool _cameraClay_1Enabled;
    [ObservableProperty] private string _cameraClay_1Name = AppConfigDefaults.DefaultCameraClay_1Name;
    [ObservableProperty] private string _cameraClay_1RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraClay_1PreviewRtspUrl = string.Empty;

    [ObservableProperty] private bool _cameraClay_2Enabled;
    [ObservableProperty] private string _cameraClay_2Name = AppConfigDefaults.DefaultCameraClay_2Name;
    [ObservableProperty] private string _cameraClay_2RtspUrl = string.Empty;
    [ObservableProperty] private string _cameraClay_2PreviewRtspUrl = string.Empty;

    // Common Parameters
    [ObservableProperty] private string _cameraPreviewDefault = AppConfigDefaults.DefaultCameraPreview;
    [ObservableProperty] private bool _isCamera1DefaultPreview;
    [ObservableProperty] private bool _isCamera2DefaultPreview;
    [ObservableProperty] private string _cameraCaptureTimeoutMs = AppConfigDefaults.DefaultCameraCaptureTimeoutMs;
    [ObservableProperty] private string _cameraCaptureJpegQuality = AppConfigDefaults.DefaultCameraCaptureJpegQuality;
    [ObservableProperty] private string _cameraCaptureWarmupFrames = AppConfigDefaults.DefaultCameraCaptureWarmupFrames;

    public bool CanManageCameraSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        // Load C2 Cameras
        Camera1Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.Camera1Enabled, CancellationToken.None), out var cam1Enabled)
            ? cam1Enabled
            : bool.Parse(AppConfigDefaults.DefaultCamera1Enabled);
        Camera1Name = await repo.GetValueAsync(AppConfigKeys.Camera1Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera1Name;
        Camera1RtspUrl = await repo.GetValueAsync(AppConfigKeys.Camera1RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera1RtspUrl;
        Camera1PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.Camera1PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera1PreviewRtspUrl;

        Camera2Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.Camera2Enabled, CancellationToken.None), out var cam2Enabled)
            ? cam2Enabled
            : bool.Parse(AppConfigDefaults.DefaultCamera2Enabled);
        Camera2Name = await repo.GetValueAsync(AppConfigKeys.Camera2Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera2Name;
        Camera2RtspUrl = await repo.GetValueAsync(AppConfigKeys.Camera2RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera2RtspUrl;
        Camera2PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.Camera2PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCamera2PreviewRtspUrl;

        // Load C6 Cameras
        CameraC6_1Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraC6_1Enabled, CancellationToken.None), out var camC6_1Enabled)
            ? camC6_1Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraC6_1Enabled);
        CameraC6_1Name = await repo.GetValueAsync(AppConfigKeys.CameraC6_1Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_1Name;
        CameraC6_1RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraC6_1RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_1RtspUrl;
        CameraC6_1PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraC6_1PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_1PreviewRtspUrl;

        CameraC6_2Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraC6_2Enabled, CancellationToken.None), out var camC6_2Enabled)
            ? camC6_2Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraC6_2Enabled);
        CameraC6_2Name = await repo.GetValueAsync(AppConfigKeys.CameraC6_2Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_2Name;
        CameraC6_2RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraC6_2RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_2RtspUrl;
        CameraC6_2PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraC6_2PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraC6_2PreviewRtspUrl;

        // Load Crusher cameras
        CameraCrusher_1Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraCrusher_1Enabled, CancellationToken.None), out var camCrusher1Enabled)
            ? camCrusher1Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraCrusher_1Enabled);
        CameraCrusher_1Name = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_1Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_1Name;
        CameraCrusher_1RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_1RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_1RtspUrl;
        CameraCrusher_1PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_1PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_1PreviewRtspUrl;

        CameraCrusher_2Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraCrusher_2Enabled, CancellationToken.None), out var camCrusher2Enabled)
            ? camCrusher2Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraCrusher_2Enabled);
        CameraCrusher_2Name = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_2Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_2Name;
        CameraCrusher_2RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_2RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_2RtspUrl;
        CameraCrusher_2PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraCrusher_2PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCrusher_2PreviewRtspUrl;

        // Load Clay cameras
        CameraClay_1Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraClay_1Enabled, CancellationToken.None), out var camClay1Enabled)
            ? camClay1Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraClay_1Enabled);
        CameraClay_1Name = await repo.GetValueAsync(AppConfigKeys.CameraClay_1Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_1Name;
        CameraClay_1RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraClay_1RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_1RtspUrl;
        CameraClay_1PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraClay_1PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_1PreviewRtspUrl;

        CameraClay_2Enabled = bool.TryParse(await repo.GetValueAsync(AppConfigKeys.CameraClay_2Enabled, CancellationToken.None), out var camClay2Enabled)
            ? camClay2Enabled
            : bool.Parse(AppConfigDefaults.DefaultCameraClay_2Enabled);
        CameraClay_2Name = await repo.GetValueAsync(AppConfigKeys.CameraClay_2Name, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_2Name;
        CameraClay_2RtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraClay_2RtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_2RtspUrl;
        CameraClay_2PreviewRtspUrl = await repo.GetValueAsync(AppConfigKeys.CameraClay_2PreviewRtspUrl, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraClay_2PreviewRtspUrl;

        // Load Common
        CameraPreviewDefault = await repo.GetValueAsync(AppConfigKeys.CameraPreviewDefault, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraPreview;
        SyncDefaultPreviewSelection(CameraPreviewDefault);
        CameraCaptureTimeoutMs = await repo.GetValueAsync(AppConfigKeys.CameraCaptureTimeoutMs, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCaptureTimeoutMs;
        CameraCaptureJpegQuality = await repo.GetValueAsync(AppConfigKeys.CameraCaptureJpegQuality, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCaptureJpegQuality;
        CameraCaptureWarmupFrames = await repo.GetValueAsync(AppConfigKeys.CameraCaptureWarmupFrames, CancellationToken.None) ?? AppConfigDefaults.DefaultCameraCaptureWarmupFrames;
    }

    [RelayCommand(CanExecute = nameof(CanManageCameraSettings))]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        try
        {
            var useCase = scope.ServiceProvider.GetRequiredService<UpdateCameraSettingsUseCase>();
            await useCase.ExecuteAsync(
                new UpdateCameraSettingsRequest(
                    Camera1Enabled,
                    Camera1Name,
                    Camera1RtspUrl,
                    Camera1PreviewRtspUrl,
                    Camera2Enabled,
                    Camera2Name,
                    Camera2RtspUrl,
                    Camera2PreviewRtspUrl,
                    CameraC6_1Enabled,
                    CameraC6_1Name,
                    CameraC6_1RtspUrl,
                    CameraC6_1PreviewRtspUrl,
                    CameraC6_2Enabled,
                    CameraC6_2Name,
                    CameraC6_2RtspUrl,
                    CameraC6_2PreviewRtspUrl,
                    CameraPreviewDefault,
                    CameraCaptureTimeoutMs,
                    CameraCaptureJpegQuality,
                    CameraCaptureWarmupFrames,
                    CameraCrusher_1Enabled,
                    CameraCrusher_1Name,
                    CameraCrusher_1RtspUrl,
                    CameraCrusher_1PreviewRtspUrl,
                    CameraCrusher_2Enabled,
                    CameraCrusher_2Name,
                    CameraCrusher_2RtspUrl,
                    CameraCrusher_2PreviewRtspUrl,
                    CameraClay_1Enabled,
                    CameraClay_1Name,
                    CameraClay_1RtspUrl,
                    CameraClay_1PreviewRtspUrl,
                    CameraClay_2Enabled,
                    CameraClay_2Name,
                    CameraClay_2RtspUrl,
                    CameraClay_2PreviewRtspUrl),
                CancellationToken.None);
            await dialogService.ShowInfoAsync("Thông báo", "Lưu cấu hình camera thành công!");
        }
        catch (InvalidOperationException ex)
        {
            await dialogService.ShowWarningAsync("Loi", ex.Message);
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình camera: {ex.Message}");
        }
    }

    partial void OnIsCamera1DefaultPreviewChanged(bool value)
    {
        if (_isUpdatingDefaultPreviewSelection)
        {
            return;
        }

        if (value)
        {
            SetDefaultPreview("CAM1");
            return;
        }

        EnsureDefaultPreviewSelection("CAM1");
    }

    partial void OnIsCamera2DefaultPreviewChanged(bool value)
    {
        if (_isUpdatingDefaultPreviewSelection)
        {
            return;
        }

        if (value)
        {
            SetDefaultPreview("CAM2");
            return;
        }

        EnsureDefaultPreviewSelection("CAM2");
    }

    partial void OnCameraPreviewDefaultChanged(string value)
    {
        if (_isUpdatingDefaultPreviewSelection)
        {
            return;
        }

        SyncDefaultPreviewSelection(value);
    }

    private void SetDefaultPreview(string cameraCode)
    {
        _isUpdatingDefaultPreviewSelection = true;
        try
        {
            CameraPreviewDefault = cameraCode;
            IsCamera1DefaultPreview = string.Equals(cameraCode, "CAM1", StringComparison.OrdinalIgnoreCase);
            IsCamera2DefaultPreview = string.Equals(cameraCode, "CAM2", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _isUpdatingDefaultPreviewSelection = false;
        }
    }

    private void SyncDefaultPreviewSelection(string? cameraCode)
    {
        var normalizedCode = string.Equals(cameraCode?.Trim(), "CAM2", StringComparison.OrdinalIgnoreCase)
            ? "CAM2"
            : "CAM1";

        _isUpdatingDefaultPreviewSelection = true;
        try
        {
            CameraPreviewDefault = normalizedCode;
            IsCamera1DefaultPreview = normalizedCode == "CAM1";
            IsCamera2DefaultPreview = normalizedCode == "CAM2";
        }
        finally
        {
            _isUpdatingDefaultPreviewSelection = false;
        }
    }

    private void EnsureDefaultPreviewSelection(string cameraCode)
    {
        var currentDefault = string.Equals(CameraPreviewDefault?.Trim(), "CAM2", StringComparison.OrdinalIgnoreCase)
            ? "CAM2"
            : "CAM1";

        if (!string.Equals(currentDefault, cameraCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SyncDefaultPreviewSelection(currentDefault);
    }
}
