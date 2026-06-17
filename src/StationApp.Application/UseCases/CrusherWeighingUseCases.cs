using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed record CreateCrusherSessionRequest(
    Guid VehicleId,
    string WeighingMode,
    decimal Weight1,
    bool Weight1IsStable,
    WeightMode Weight1Mode);

public sealed record CaptureCrusherWeight2Request(
    Guid SessionId,
    decimal Weight2,
    bool Weight2IsStable,
    WeightMode Weight2Mode);

public sealed class CrusherWeighingUseCases
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IWeighingSessionRepository _sessionRepository;
    private readonly IWeighingSessionNumberGenerator _sessionNoGenerator;
    private readonly IStationScope _stationScope;
    private readonly IStationOperationSettingsRepository _operationSettings;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CrusherWeighingUseCases(
        IVehicleRepository vehicleRepository,
        IWeighingSessionRepository sessionRepository,
        IWeighingSessionNumberGenerator sessionNoGenerator,
        IStationScope stationScope,
        IStationOperationSettingsRepository operationSettings,
        IClock clock,
        ICurrentUserContext currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _sessionRepository = sessionRepository;
        _sessionNoGenerator = sessionNoGenerator;
        _stationScope = stationScope;
        _operationSettings = operationSettings;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<InternalVehicleOptionDto>> SearchInternalVehiclesAsync(string? keyword, CancellationToken ct)
    {
        var vehicles = await _vehicleRepository.SearchInternalVehiclesAsync(keyword, 50, ct);
        return vehicles
            .Select(x => new InternalVehicleOptionDto(
                x.Id,
                x.VehiclePlate,
                x.DriverName,
                x.TtcpWeight,
                x.StandardTareSource))
            .ToList();
    }

    public Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchSessionsAsync(string? keyword, CancellationToken ct)
        => _sessionRepository.SearchCrusherSessionsAsync(keyword, ct);

    public async Task<string> GetDefaultWeighingModeAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var value = await _operationSettings.GetValueAsync(stationCode, StationOperationSettingKeys.CrusherDefaultWeighMode, ct);
        return NormalizeMode(value);
    }

    public async Task<Guid> CreateSessionAsync(CreateCrusherSessionRequest request, CancellationToken ct)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, ct);
        if (vehicle is null || !vehicle.IsInternalVehicle)
        {
            throw new InvalidOperationException("Không tìm thấy xe nội bộ hợp lệ cho trạm đập.");
        }

        var mode = NormalizeMode(request.WeighingMode);
        if (mode == CrusherWeighingModes.SingleWithStandardTare && !await IsSingleWeighEnabledAsync(ct))
        {
            throw new InvalidOperationException("Trạm hiện tại chưa bật chế độ cân một lần bằng trọng lượng xe chuẩn.");
        }

        if (mode == CrusherWeighingModes.SingleWithStandardTare && (!vehicle.TtcpWeight.HasValue || vehicle.TtcpWeight.Value <= 0))
        {
            throw new InvalidOperationException("Xe nội bộ chưa có trọng lượng xe chuẩn, không thể cân một lần.");
        }

        var now = _clock.NowLocal;
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            StationCode = stationCode,
            SessionNo = await _sessionNoGenerator.GenerateAsync(TransactionType.INBOUND, ct),
            TransactionType = TransactionType.INBOUND,
            VehiclePlate = vehicle.VehiclePlate,
            InternalVehicleNo = vehicle.VehiclePlate,
            DriverName = vehicle.DriverName,
            Weight1 = RoundWeight(request.Weight1),
            Weight1Time = now,
            Weight2 = mode == CrusherWeighingModes.SingleWithStandardTare
                ? vehicle.TtcpWeight
                : null,
            Weight2Time = mode == CrusherWeighingModes.SingleWithStandardTare
                ? now
                : null,
            Ttcp10WeightSnapshot = vehicle.TtcpWeight,
            StandardTareVehicleId = vehicle.Id,
            StandardTareWeightSnapshot = vehicle.TtcpWeight,
            StandardTareSourceSnapshot = vehicle.StandardTareSource,
            WeighingMode = mode,
            NetWeightCalculationMode = mode == CrusherWeighingModes.SingleWithStandardTare
                ? NetWeightCalculationModes.Weight1MinusStandardTare
                : NetWeightCalculationModes.Weight2Diff,
            SessionStatus = mode == CrusherWeighingModes.SingleWithStandardTare
                ? WeighingSessionStatus.COMPLETED
                : WeighingSessionStatus.PENDING_WEIGHT2,
            NetWeight = mode == CrusherWeighingModes.SingleWithStandardTare
                ? Math.Max(0, RoundWeight(request.Weight1) - vehicle.TtcpWeight!.Value)
                : null,
            IsOverweight = false,
            OverweightAmount = 0,
            OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            CreatedAt = now,
            CreatedBy = CurrentUsername()
        };

        await _sessionRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task CaptureWeight2Async(CaptureCrusherWeight2Request request, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân trạm đập.");

        if (!string.Equals(session.WeighingMode, CrusherWeighingModes.TwoWeigh, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Lượt cân một lần không cần cân lần 2.");
        }

        if (session.Weight1 is null)
        {
            throw new InvalidOperationException("Lượt cân chưa có cân lần 1.");
        }

        if (session.SessionStatus == WeighingSessionStatus.COMPLETED)
        {
            throw new InvalidOperationException("Lượt cân đã hoàn tất, không thể cân lần 2 lại.");
        }

        var now = _clock.NowLocal;
        session.Weight2 = RoundWeight(request.Weight2);
        session.Weight2Time = now;
        session.NetWeight = Math.Abs(session.Weight2.Value - session.Weight1.Value);
        session.NetWeightCalculationMode = NetWeightCalculationModes.Weight2Diff;
        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.IsOverweight = false;
        session.OverweightAmount = 0;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.UpdatedAt = now;
        session.UpdatedBy = CurrentUsername();

        await _sessionRepository.UpdateAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string NormalizeMode(string? mode)
        => string.Equals(mode, CrusherWeighingModes.SingleWithStandardTare, StringComparison.OrdinalIgnoreCase)
            ? CrusherWeighingModes.SingleWithStandardTare
            : CrusherWeighingModes.TwoWeigh;

    private static decimal RoundWeight(decimal value)
        => decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    private async Task<bool> IsSingleWeighEnabledAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var value = await _operationSettings.GetValueAsync(stationCode, StationOperationSettingKeys.CrusherSingleWeighEnabled, ct);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private string CurrentUsername()
        => string.IsNullOrWhiteSpace(_currentUser.Username) ? "SYSTEM" : _currentUser.Username;
}
