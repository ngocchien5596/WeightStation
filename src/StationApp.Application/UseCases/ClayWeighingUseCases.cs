using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed record CreateClaySessionRequest(
    Guid VehicleId,
    string WeighingMode,
    decimal Weight1,
    bool Weight1IsStable,
    WeightMode Weight1Mode,
    // Crusher Weighing: Product and Customer Information
    string? ProductCode,
    string? ProductName,
    string? CustomerCode,
    string? CustomerName
);

public sealed record CaptureClayWeight2Request(
    Guid SessionId,
    decimal Weight2,
    bool Weight2IsStable,
    WeightMode Weight2Mode);

public sealed class ClayWeighingUseCases
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWeighingSessionRepository _sessionRepository;
    private readonly IWeighingSessionNumberGenerator _sessionNoGenerator;
    private readonly IStationScope _stationScope;
    private readonly IStationOperationSettingsRepository _operationSettings;
    private readonly ISyncOutboxRepository _syncOutboxRepository;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ClayWeighingUseCases(
        IVehicleRepository vehicleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IWeighingSessionRepository sessionRepository,
        IWeighingSessionNumberGenerator sessionNoGenerator,
        IStationScope stationScope,
        IStationOperationSettingsRepository operationSettings,
        ISyncOutboxRepository syncOutboxRepository,
        ISyncPayloadFactory syncPayloadFactory,
        IClock clock,
        ICurrentUserContext currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _sessionRepository = sessionRepository;
        _sessionNoGenerator = sessionNoGenerator;
        _stationScope = stationScope;
        _operationSettings = operationSettings;
        _syncOutboxRepository = syncOutboxRepository;
        _syncPayloadFactory = syncPayloadFactory;
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

    public Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchSessionsAsync(string? keyword, DateTime? selectedDate, CancellationToken ct)
        => _sessionRepository.SearchClaySessionsAsync(keyword, selectedDate, ct);

    public async Task<string> GetDefaultWeighingModeAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var value = await _operationSettings.GetValueAsync(stationCode, ClayStationOperationSettingKeys.ClayDefaultWeighMode, ct);
        return NormalizeMode(value);
    }

    public async Task<Guid> CreateSessionAsync(CreateClaySessionRequest request, CancellationToken ct)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, ct);
        if (vehicle is null || !vehicle.IsInternalVehicle)
        {
            throw new InvalidOperationException("Không tìm thấy xe nội bộ hợp lệ cho trạm đập.");
        }

        var mode = NormalizeMode(request.WeighingMode);
        if (mode == ClayWeighingModes.SingleWithStandardTare && !await IsSingleWeighEnabledAsync(ct))
        {
            throw new InvalidOperationException("Trạm hiện tại chưa bật chế độ cân một lần bằng trọng lượng xe chuẩn.");
        }

        if (mode == ClayWeighingModes.SingleWithStandardTare && (!vehicle.TtcpWeight.HasValue || vehicle.TtcpWeight.Value <= 0))
        {
            throw new InvalidOperationException("Xe nội bộ chưa có trọng lượng xe chuẩn, không thể cân một lần.");
        }

        var now = _clock.NowLocal;
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        await EnsureCustomerAsync(request.CustomerCode, request.CustomerName, now, ct);
        await EnsureProductAsync(request.ProductCode, request.ProductName, now, ct);

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
            Weight2 = mode == ClayWeighingModes.SingleWithStandardTare
                ? vehicle.TtcpWeight
                : null,
            Weight2Time = mode == ClayWeighingModes.SingleWithStandardTare
                ? now
                : null,
            Ttcp10WeightSnapshot = vehicle.TtcpWeight,
            StandardTareVehicleId = vehicle.Id,
            StandardTareWeightSnapshot = vehicle.TtcpWeight,
            StandardTareSourceSnapshot = vehicle.StandardTareSource,
            // Crusher Weighing: Product and Customer Information
            ProductCode = request.ProductCode,
            ProductName = request.ProductName,
            CustomerCode = request.CustomerCode,
            CustomerName = request.CustomerName,
            WeighingMode = mode,
            NetWeightCalculationMode = mode == ClayWeighingModes.SingleWithStandardTare
                ? NetWeightCalculationModes.Weight1MinusStandardTare
                : NetWeightCalculationModes.Weight2Diff,
            SessionStatus = mode == ClayWeighingModes.SingleWithStandardTare
                ? WeighingSessionStatus.COMPLETED
                : WeighingSessionStatus.PENDING_WEIGHT2,
            NetWeight = mode == ClayWeighingModes.SingleWithStandardTare
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

    public async Task CaptureWeight2Async(CaptureClayWeight2Request request, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân trạm đập.");

        if (!string.Equals(session.WeighingMode, ClayWeighingModes.TwoWeigh, StringComparison.OrdinalIgnoreCase))
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

    private async Task EnsureCustomerAsync(string? customerCode, string? customerName, DateTime now, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(customerCode);
        var normalizedName = NormalizeOptional(customerName);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var existing = await _customerRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            existing = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = normalizedCode,
                CustomerName = normalizedName,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = CurrentUsername()
            };
            await _customerRepository.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedName)
            && !string.Equals(existing.CustomerName, normalizedName, StringComparison.Ordinal))
        {
            existing.CustomerName = normalizedName;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = now;
        existing.UpdatedBy = CurrentUsername();
        await _customerRepository.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnsureProductAsync(string? productCode, string? productName, DateTime now, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(productCode);
        var normalizedName = NormalizeOptional(productName);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var productType = ProductTypes.InferForTransaction(TransactionType.INBOUND);
        var existing = await _productRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            existing = new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = normalizedCode,
                ProductName = normalizedName,
                ProductType = productType,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = CurrentUsername()
            };
            await _productRepository.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedName)
            && !string.Equals(existing.ProductName, normalizedName, StringComparison.Ordinal))
        {
            existing.ProductName = normalizedName;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(productType)
            && !string.Equals(existing.ProductType, productType, StringComparison.Ordinal))
        {
            existing.ProductType = productType;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = now;
        existing.UpdatedBy = CurrentUsername();
        await _productRepository.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnqueueMasterSyncAsync(
        Guid aggregateId,
        string aggregateType,
        string payloadJson,
        DateTime now,
        CancellationToken ct)
    {
        await _syncOutboxRepository.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            PayloadJson = payloadJson,
            IdempotencyKey = aggregateId,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeMode(string? mode)
        => string.Equals(mode, ClayWeighingModes.SingleWithStandardTare, StringComparison.OrdinalIgnoreCase)
            ? ClayWeighingModes.SingleWithStandardTare
            : ClayWeighingModes.TwoWeigh;

    private static decimal RoundWeight(decimal value)
        => decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    private async Task<bool> IsSingleWeighEnabledAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var value = await _operationSettings.GetValueAsync(stationCode, ClayStationOperationSettingKeys.ClaySingleWeighEnabled, ct);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private string CurrentUsername()
        => string.IsNullOrWhiteSpace(_currentUser.Username) ? "SYSTEM" : _currentUser.Username;
}
