using System.Globalization;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Constants;

namespace StationApp.Application.UseCases;

public sealed class UpdateSystemSettingsUseCase
{
    private readonly IAppConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public UpdateSystemSettingsUseCase(
        IAppConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(UpdateSystemSettingsRequest request, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "update system settings");

        if (!decimal.TryParse(request.OverweightSplitStepWeight.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var splitStepWeight)
            || splitStepWeight <= 0.0001m)
        {
            throw new InvalidOperationException("OverweightSplitStepWeight must be greater than 0.0001.");
        }

        await _configRepository.SetValueAsync("station_code", request.StationCode.Trim(), ct);
        await _configRepository.SetValueAsync("ticket_prefix", request.TicketPrefix.Trim(), ct);
        await _configRepository.SetValueAsync("delivery_prefix", request.DeliveryPrefix.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.ToleranceKgPerBag, request.ToleranceKgPerBag.Trim(), ct);
        await _configRepository.SetValueAsync("sync_interval", request.SyncIntervalSeconds.Trim(), ct);
        await _configRepository.SetValueAsync("registration_inbound_poll_seconds", request.RegistrationInboundPollSeconds.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.OverweightSplitStepWeight, request.OverweightSplitStepWeight.Trim(), ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class UpdateCameraSettingsUseCase
{
    private readonly IAppConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public UpdateCameraSettingsUseCase(
        IAppConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(UpdateCameraSettingsRequest request, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "update camera settings");

        await _configRepository.SetValueAsync(AppConfigKeys.Camera1Enabled, request.Camera1Enabled.ToString().ToLowerInvariant(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera1Name, request.Camera1Name.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera1RtspUrl, request.Camera1RtspUrl.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera1PreviewRtspUrl, request.Camera1PreviewRtspUrl.Trim(), ct);

        await _configRepository.SetValueAsync(AppConfigKeys.Camera2Enabled, request.Camera2Enabled.ToString().ToLowerInvariant(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera2Name, request.Camera2Name.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera2RtspUrl, request.Camera2RtspUrl.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.Camera2PreviewRtspUrl, request.Camera2PreviewRtspUrl.Trim(), ct);

        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_1Enabled, request.CameraC6_1Enabled.ToString().ToLowerInvariant(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_1Name, request.CameraC6_1Name.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_1RtspUrl, request.CameraC6_1RtspUrl.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_1PreviewRtspUrl, request.CameraC6_1PreviewRtspUrl.Trim(), ct);

        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_2Enabled, request.CameraC6_2Enabled.ToString().ToLowerInvariant(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_2Name, request.CameraC6_2Name.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_2RtspUrl, request.CameraC6_2RtspUrl.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraC6_2PreviewRtspUrl, request.CameraC6_2PreviewRtspUrl.Trim(), ct);

        await _configRepository.SetValueAsync(AppConfigKeys.CameraPreviewDefault, request.CameraPreviewDefault.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraCaptureTimeoutMs, request.CameraCaptureTimeoutMs.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraCaptureJpegQuality, request.CameraCaptureJpegQuality.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.CameraCaptureWarmupFrames, request.CameraCaptureWarmupFrames.Trim(), ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class UpdateScaleDeviceSettingsUseCase
{
    private readonly IAppConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public UpdateScaleDeviceSettingsUseCase(
        IAppConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(UpdateScaleDeviceSettingsRequest request, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "update scale device settings");

        await _configRepository.SetValueAsync(AppConfigKeys.DeviceComPort, request.ComPort.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceBaudrate, request.Baudrate.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceParity, request.Parity.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceDataBits, request.DataBits.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceStopBits, request.StopBits.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceParserType, request.ParserType.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceFrameEndChar, request.FrameEndChar.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.DeviceStableCycles, request.StableCycles.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.WeightSubstringStart, request.WeightSubstringStart.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.WeightSubstringLength, request.WeightSubstringLength.Trim(), ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
