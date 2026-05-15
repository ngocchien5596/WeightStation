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
        await _configRepository.SetValueAsync("tolerance_kg", request.ToleranceKg.Trim(), ct);
        await _configRepository.SetValueAsync("sync_interval", request.SyncIntervalSeconds.Trim(), ct);
        await _configRepository.SetValueAsync("registration_inbound_poll_seconds", request.RegistrationInboundPollSeconds.Trim(), ct);
        await _configRepository.SetValueAsync(AppConfigKeys.OverweightSplitStepWeight, request.OverweightSplitStepWeight.Trim(), ct);
        await _configRepository.SetValueAsync("pilot_mode_enabled", request.PilotModeEnabled ? "true" : "false", ct);

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

        await _configRepository.SetValueAsync("device_com_port", request.ComPort.Trim(), ct);
        await _configRepository.SetValueAsync("device_baudrate", request.Baudrate.Trim(), ct);
        await _configRepository.SetValueAsync("device_parser_type", request.ParserType.Trim(), ct);
        await _configRepository.SetValueAsync("device_frame_end_char", request.FrameEndChar.Trim(), ct);
        await _configRepository.SetValueAsync("weight_substring_start", request.WeightSubstringStart.Trim(), ct);
        await _configRepository.SetValueAsync("weight_substring_length", request.WeightSubstringLength.Trim(), ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
