using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Services;
using System.Text.Json;

using StationApp.Application.UseCases.MasterData;

namespace StationApp.Application.UseCases;

public sealed class CreateClayTemporaryCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly EnsureInboundMasterDataUseCase _ensureMasterDataUseCase;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateClayTemporaryCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        EnsureInboundMasterDataUseCase ensureMasterDataUseCase,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _ensureMasterDataUseCase = ensureMasterDataUseCase;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<Guid> ExecuteAsync(CreateClayVesselRequest request, CancellationToken ct)
    {
        var vesselName = NormalizeOptional(request.VesselName);
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            vesselName = await _cutOrderRepo.GenerateClayVesselDisplayCodeAsync(ct);
        }

        var productName = RequireText(request.ProductName, "Hàng hóa");
        var now = _clock.NowLocal;
        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            CutOrderSource = CutOrderSource.MANUAL,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            TransactionType = TransactionType.INBOUND,
            TransportMethod = TransportMethod.WATERWAY,
            VehiclePlate = vesselName,
            CustomerCode = NormalizeOptional(request.CustomerCode),
            CustomerName = NormalizeOptional(request.CustomerName),
            ProductCode = NormalizeOptional(request.ProductCode),
            ProductName = productName,
            ProductType = ProductTypes.InferForTransaction(TransactionType.INBOUND),
            PlannedWeight = null,
            Notes = NormalizeOptional(request.Notes),
            ProcessingStage = ProcessingStage.WEIGHING,
            IsExportScale = false,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        await _uow.ExecuteInTransactionAsync(
            async innerCt =>
            {
                await _ensureMasterDataUseCase.EnsureCustomerAsync(cutOrder.CustomerCode, cutOrder.CustomerName, innerCt);
                await _ensureMasterDataUseCase.EnsureProductAsync(cutOrder.ProductCode, cutOrder.ProductName, null, TransactionType.INBOUND, innerCt);
                await _cutOrderRepo.AddAsync(cutOrder, innerCt);
            },
            ct);

        return cutOrder.Id;
    }

    private static string RequireText(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} là bắt buộc.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateClayVesselUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly EnsureInboundMasterDataUseCase _ensureMasterDataUseCase;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepo;

    public UpdateClayVesselUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        EnsureInboundMasterDataUseCase ensureMasterDataUseCase,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepo)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _ensureMasterDataUseCase = ensureMasterDataUseCase;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepo = auditLogRepo;
    }

    public async Task ExecuteAsync(UpdateClayVesselRequest request, CancellationToken ct)
    {
        var vesselName = RequireText(request.VesselName, "Tên tàu/sà lan");
        var customerCode = RequireText(request.CustomerCode, "Mã đơn vị vận chuyển");
        var customerName = RequireText(request.CustomerName, "Đơn vị vận chuyển");
        var productCode = RequireText(request.ProductCode, "Mã hàng");
        var productName = RequireText(request.ProductName, "Hàng hóa");
        var notes = NormalizeOptional(request.Notes);

        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu mỏ sét.");
        ValidateEditableClayVessel(cutOrder);

        var trips = await _cutOrderRepo.GetClayVehicleTripsAsync(cutOrder.Id, ct);
        var sessions = new List<WeighingSession>();
        var lines = new List<WeighingSessionLine>();

        foreach (var trip in trips)
        {
            var session = await _sessionRepo.GetByIdAsync(trip.SessionId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy chuyến xe thuộc tàu mỏ sét.");
            sessions.Add(session);

            var sessionLines = (await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == cutOrder.Id)
                .ToList();
            lines.AddRange(sessionLines);
        }

        var oldState = CreateAuditSnapshot(cutOrder);
        var now = _clock.NowLocal;
        var username = _userContext.Username;

        cutOrder.VehiclePlate = vesselName;
        cutOrder.CustomerCode = customerCode;
        cutOrder.CustomerName = customerName;
        cutOrder.ProductCode = productCode;
        cutOrder.ProductName = productName;
        cutOrder.ProductType = ProductTypes.InferForTransaction(TransactionType.INBOUND);
        cutOrder.Notes = notes;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = username;

        foreach (var session in sessions)
        {
            session.CustomerCode = customerCode;
            session.CustomerName = customerName;
            session.ProductCode = productCode;
            session.ProductName = productName;
            session.SyncStatus = SyncStatus.SYNC_QUEUED;
            session.LastSyncAttemptAt = null;
            session.LastSyncError = null;
            session.UpdatedAt = now;
            session.UpdatedBy = username;
        }

        foreach (var line in lines)
        {
            line.CustomerCode = customerCode;
            line.CustomerName = customerName;
            line.DistributorCode = customerCode;
            line.DistributorName = customerName;
            line.ProductCode = productCode;
            line.ProductName = productName;
            line.SyncStatus = SyncStatus.SYNC_QUEUED;
            line.LastSyncAttemptAt = null;
            line.LastSyncError = null;
            line.UpdatedAt = now;
            line.UpdatedBy = username;
        }

        var newState = CreateAuditSnapshot(cutOrder);
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = username,
            Action = "UPDATE_CLAY_VESSEL",
            EntityType = nameof(CutOrder),
            EntityId = cutOrder.Id,
            DetailJson = JsonSerializer.Serialize(new
            {
                VesselName = cutOrder.VehiclePlate,
                Old = oldState,
                New = newState,
                UpdatedSessionCount = sessions.Count,
                UpdatedLineCount = lines.Count
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var session in sessions)
            {
                await _sessionRepo.UpdateAsync(session, innerCt);
            }

            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            await _ensureMasterDataUseCase.EnsureCustomerAsync(cutOrder.CustomerCode, cutOrder.CustomerName, innerCt);
            await _ensureMasterDataUseCase.EnsureProductAsync(cutOrder.ProductCode, cutOrder.ProductName, null, TransactionType.INBOUND, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
            await _auditLogRepo.AddAsync(auditLog, innerCt);
        }, ct);
    }

    private static void ValidateEditableClayVessel(CutOrder cutOrder)
    {
        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Tàu mỏ sét đã bị hủy hoặc xóa.");
        }

        if (cutOrder.IsExportScale
            || cutOrder.TransactionType != TransactionType.INBOUND
            || cutOrder.TransportMethod != TransportMethod.WATERWAY)
        {
            throw new InvalidOperationException("Cắt lệnh không thuộc luồng tàu mỏ sét.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Tàu đã chốt tổng, không thể sửa.");
        }
    }

    private static object CreateAuditSnapshot(CutOrder cutOrder)
        => new
        {
            cutOrder.VehiclePlate,
            cutOrder.CustomerCode,
            cutOrder.CustomerName,
            cutOrder.ProductCode,
            cutOrder.ProductName,
            cutOrder.Notes
        };

    private static string RequireText(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} là bắt buộc.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateClayVehicleSessionUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ClayWeighingUseCases _clayWeighingUseCases;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateClayVehicleSessionUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        ClayWeighingUseCases clayWeighingUseCases,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _clayWeighingUseCases = clayWeighingUseCases;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<CreateClayVehicleSessionResult> ExecuteAsync(CreateClayVehicleSessionRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu mỏ sét.");
        ValidateOpenClayVessel(cutOrder);

        var vehicle = await _vehicleRepo.GetByIdAsync(request.VehicleId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy xe nội bộ.");
        if (!vehicle.IsInternalVehicle || !vehicle.IsActive)
        {
            throw new InvalidOperationException("Chỉ được cân xe nội bộ đang hoạt động cho mỏ sét.");
        }

        var sessionId = await _clayWeighingUseCases.CreateSessionAsync(
            new CreateClaySessionRequest(
                request.VehicleId,
                request.WeighingMode,
                request.Weight1,
                request.Weight1IsStable,
                request.Weight1Mode,
                cutOrder.ProductCode,
                cutOrder.ProductName,
                cutOrder.CustomerCode,
                cutOrder.CustomerName),
            ct);

        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân vừa tạo.");

        var now = _clock.NowLocal;
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = cutOrder.Id,
            SequenceNo = 1,
            CustomerCode = cutOrder.CustomerCode,
            CustomerName = cutOrder.CustomerName,
            DistributorName = cutOrder.CustomerName,
            ProductCode = cutOrder.ProductCode,
            ProductName = cutOrder.ProductName,
            ActualAllocatedWeight = session.SessionStatus == WeighingSessionStatus.COMPLETED ? session.NetWeight : null,
            LineStatus = session.SessionStatus == WeighingSessionStatus.COMPLETED
                ? WeighingSessionLineStatus.ALLOCATED
                : WeighingSessionLineStatus.PENDING,
            HasPrintedDeliveryTicket = false,
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.AddLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
        }, ct);

        return new CreateClayVehicleSessionResult(session.Id, session.SessionNo);
    }

    private static void ValidateOpenClayVessel(CutOrder cutOrder)
    {
        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Tàu mỏ sét đã bị hủy hoặc xóa.");
        }

        if (cutOrder.IsExportScale
            || cutOrder.TransactionType != TransactionType.INBOUND
            || cutOrder.TransportMethod != TransportMethod.WATERWAY)
        {
            throw new InvalidOperationException("Cắt lệnh không thuộc luồng tàu mỏ sét.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Tàu đã chốt, không thể tạo thêm chuyến xe.");
        }
    }
}

public sealed class CreateClayPendingVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighingSessionNumberGenerator _sessionNoGenerator;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateClayPendingVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        IWeighingSessionNumberGenerator sessionNoGenerator,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _sessionNoGenerator = sessionNoGenerator;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<CreateClayVehicleSessionResult> ExecuteAsync(CreateClayPendingVehicleTripRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu mỏ sét.");
        if (cutOrder.IsDeleted || cutOrder.IsCancelled || cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Tàu mỏ sét không hợp lệ hoặc đã chốt.");
        }

        var vehicle = await _vehicleRepo.GetByIdAsync(request.VehicleId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy xe nội bộ.");
        if (!vehicle.IsInternalVehicle || !vehicle.IsActive)
        {
            throw new InvalidOperationException("Chỉ được tạo chuyến cho xe nội bộ đang hoạt động.");
        }

        var standardTare = StandardTarePolicy.GetEffectiveStandardTare(vehicle, _clock.TodayLocal);
        var mode = ClayWeighingModes.TwoWeigh;

        var now = _clock.NowLocal;
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            StationCode = cutOrder.StationCode,
            SessionNo = await _sessionNoGenerator.GenerateAsync(TransactionType.INBOUND, ct),
            TransactionType = TransactionType.INBOUND,
            VehiclePlate = vehicle.VehiclePlate,
            InternalVehicleNo = vehicle.VehiclePlate,
            DriverName = vehicle.DriverName,
            Weight2 = null,
            Weight2Time = null,
            StandardTareVehicleId = vehicle.Id,
            StandardTareWeightSnapshot = standardTare,
            StandardTareSourceSnapshot = vehicle.StandardTareSource,
            ProductCode = cutOrder.ProductCode,
            ProductName = cutOrder.ProductName,
            CustomerCode = cutOrder.CustomerCode,
            CustomerName = cutOrder.CustomerName,
            WeighingMode = mode,
            NetWeightCalculationMode = NetWeightCalculationModes.Weight2Diff,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
            OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = cutOrder.Id,
            SequenceNo = 1,
            CustomerCode = cutOrder.CustomerCode,
            CustomerName = cutOrder.CustomerName,
            DistributorCode = cutOrder.CustomerCode,
            DistributorName = cutOrder.CustomerName,
            ProductCode = cutOrder.ProductCode,
            ProductName = cutOrder.ProductName,
            LineStatus = WeighingSessionLineStatus.PENDING,
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.AddAsync(session, innerCt);
            await _sessionRepo.AddLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
        }, ct);

        return new CreateClayVehicleSessionResult(session.Id, session.SessionNo);
    }
}

public sealed class CaptureClayWeight1ForTripUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CaptureClayWeight1ForTripUseCase(
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(CaptureClayWeight1ForTripRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe mỏ sét.");
        if (session.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1)
        {
            throw new InvalidOperationException("Chuyến xe không ở trạng thái chờ cân lần 1.");
        }

        var now = _clock.NowLocal;
        session.Weight1 = decimal.Round(request.Weight1, 3, MidpointRounding.AwayFromZero);
        session.Weight1Time = now;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        if (string.Equals(session.WeighingMode, ClayWeighingModes.SingleWithStandardTare, StringComparison.OrdinalIgnoreCase))
        {
            var tare = session.Weight2 ?? session.StandardTareWeightSnapshot
                ?? throw new InvalidOperationException("Chuyến xe chưa có TL bì để cân 1 lần.");
            session.Weight2 = tare;
            session.Weight2Time = now;
            session.NetWeight = Math.Max(0m, session.Weight1.Value - tare);
            session.NetWeightCalculationMode = NetWeightCalculationModes.Weight1MinusStandardTare;
            session.SessionStatus = WeighingSessionStatus.COMPLETED;

            if (session.StandardTareVehicleId.HasValue)
            {
                var vehicle = await _vehicleRepo.GetByIdAsync(session.StandardTareVehicleId.Value, ct);
                if (vehicle != null)
                {
                    vehicle.TtcpWeight = tare;
                    vehicle.StandardTareUpdatedAt = now;
                    vehicle.StandardTareUpdatedBy = _userContext.Username;
                    vehicle.UpdatedAt = now;
                    vehicle.UpdatedBy = _userContext.Username;
                    await _vehicleRepo.UpdateAsync(vehicle, ct);
                }
            }
        }
        else
        {
            session.SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2;
        }

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateAsync(session, innerCt),
            ct);
    }
}

public sealed class CompleteClayVehicleSessionLineUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CompleteClayVehicleSessionLineUseCase(
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân mỏ sét.");
        if (session.SessionStatus != WeighingSessionStatus.COMPLETED)
        {
            return;
        }

        var line = (await _sessionRepo.GetLinesBySessionIdAsync(sessionId, ct)).FirstOrDefault();
        if (line == null)
        {
            return;
        }

        var now = _clock.NowLocal;
        line.ActualAllocatedWeight = session.NetWeight;
        line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateLineAsync(line, innerCt),
            ct);
    }
}

public sealed class FinalizeClayCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public FinalizeClayCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(FinalizeClayCutOrderRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu mỏ sét.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Tàu mỏ sét đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetClayVehicleTripsAsync(cutOrder.Id, ct);
        if (trips.Any(x => x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1
                || x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                || x.SessionStatus == WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("Không thể chốt khi còn chuyến xe dở dang.");
        }

        var totalWeight = trips
            .Where(x => x.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
                || x.SessionStatus == WeighingSessionStatus.COMPLETED)
            .Sum(x => x.ActualAllocatedWeight ?? x.NetWeight ?? 0m);
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("Chưa có chuyến xe hợp lệ để chốt số lượng.");
        }

        var now = _clock.NowLocal;
        cutOrder.ExportFinalizedWeight = totalWeight;
        cutOrder.ExportFinalizedAt = now;
        cutOrder.ExportFinalizedBy = _userContext.Username;
        cutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        cutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}

public sealed class TransferClayVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepo;

    public TransferClayVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepo)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepo = auditLogRepo;
    }

    public async Task ExecuteAsync(TransferClayVehicleTripRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe mỏ sét cần chuyển.");
        if (session.IsDeleted || session.IsCancelled || session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến xe đã bị hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var line = lines.Where(x => !x.IsDeleted).SingleOrDefault()
            ?? throw new InvalidOperationException("Chuyến xe mỏ sét không có dòng tàu hợp lệ.");

        var source = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu nguồn của chuyến xe.");
        var target = await _cutOrderRepo.GetByIdAsync(request.TargetCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu đích.");

        ValidateOpenClayVessel(source, "nguồn");
        ValidateOpenClayVessel(target, "đích");
        if (source.Id == target.Id)
        {
            return;
        }

        var now = _clock.NowLocal;
        line.CutOrderId = target.Id;
        line.CustomerCode = target.CustomerCode;
        line.CustomerName = target.CustomerName;
        line.DistributorCode = target.CustomerCode;
        line.DistributorName = target.CustomerName;
        line.ProductCode = target.ProductCode;
        line.ProductName = target.ProductName;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        source.SyncStatus = SyncStatus.SYNC_QUEUED;
        source.UpdatedAt = now;
        source.UpdatedBy = _userContext.Username;
        target.SyncStatus = SyncStatus.SYNC_QUEUED;
        target.UpdatedAt = now;
        target.UpdatedBy = _userContext.Username;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TRANSFER_CLAY_TRIP",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(new
            {
                SessionNo = session.SessionNo,
                VehiclePlate = session.VehiclePlate,
                SourceCutOrderId = source.Id,
                SourceVesselName = source.VehiclePlate,
                TargetCutOrderId = target.Id,
                TargetVesselName = target.VehiclePlate,
                Weight1 = session.Weight1,
                Weight2 = session.Weight2,
                NetWeight = session.NetWeight
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(source, innerCt);
            await _cutOrderRepo.UpdateAsync(target, innerCt);
            await _auditLogRepo.AddAsync(auditLog, innerCt);
        }, ct);
    }

    private static void ValidateOpenClayVessel(CutOrder cutOrder, string role)
    {
        if (cutOrder.IsDeleted || cutOrder.IsCancelled
            || cutOrder.IsExportScale
            || cutOrder.TransactionType != TransactionType.INBOUND
            || cutOrder.TransportMethod != TransportMethod.WATERWAY)
        {
            throw new InvalidOperationException($"Tàu {role} không hợp lệ.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException($"Không thể chuyển chuyến với tàu {role} đã chốt.");
        }
    }
}

public sealed class DeleteClayVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepo;

    public DeleteClayVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepo)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepo = auditLogRepo;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe mỏ sét cần xóa.");

        if (session.Weight2.HasValue || session.Weight2Time.HasValue)
        {
            throw new InvalidOperationException("Chỉ được xóa chuyến xe chưa cân lần 2.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var line = lines.Where(x => !x.IsDeleted).SingleOrDefault()
            ?? throw new InvalidOperationException("Chuyến xe mỏ sét không có dòng tàu hợp lệ.");
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu của chuyến xe.");
        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể xóa chuyến xe thuộc tàu đã chốt.");
        }

        var now = _clock.NowLocal;
        session.IsDeleted = true;
        session.IsCancelled = true;
        session.SessionStatus = WeighingSessionStatus.CANCELLED;
        session.DeletedAt = now;
        session.DeletedBy = _userContext.Username;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        line.IsDeleted = true;
        line.DeletedAt = now;
        line.DeletedBy = _userContext.Username;
        line.LineStatus = WeighingSessionLineStatus.CANCELLED;
        line.ActualAllocatedWeight = null;
        line.IsReturnedBrokenTrip = false;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "DELETE_CLAY_TRIP",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(new
            {
                SessionNo = session.SessionNo,
                VehiclePlate = session.VehiclePlate,
                VesselName = cutOrder.VehiclePlate
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            await _sessionRepo.UpdateLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
            await _auditLogRepo.AddAsync(auditLog, innerCt);
        }, ct);
    }
}

public sealed class ToggleClayReturnedBrokenTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepo;

    public ToggleClayReturnedBrokenTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepo)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepo = auditLogRepo;
    }

    public async Task ExecuteAsync(Guid sessionLineId, bool isReturnedBrokenTrip, CancellationToken ct)
    {
        var line = await _sessionRepo.GetLineByIdAsync(sessionLineId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dòng chuyến xe mỏ sét.");
        var session = await _sessionRepo.GetByIdAsync(line.WeighingSessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân mỏ sét.");
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy tàu mỏ sét.");

        if (line.IsDeleted || session.IsDeleted || session.IsCancelled || cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Chuyến xe không còn hợp lệ để cập nhật trạng thái Hoàn.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể cập nhật Hoàn cho tàu đã chốt.");
        }

        if ((line.ActualAllocatedWeight ?? 0m) <= 0m)
        {
            throw new InvalidOperationException("Chỉ được đánh dấu Hoàn khi chuyến đã có trọng lượng hàng.");
        }

        if (line.IsReturnedBrokenTrip == isReturnedBrokenTrip)
        {
            return;
        }

        ReturnedBrokenTripPreviousTripInfo? previousTrip = null;
        ReturnedBrokenTripWeightResolution? resolution = null;
        var oldState = line.IsReturnedBrokenTrip;
        var oldAllocatedWeight = line.ActualAllocatedWeight;
        var actualReturnedWeight = ResolveActualAllocatedWeight(session, line);

        if (isReturnedBrokenTrip)
        {
            previousTrip = await _sessionRepo.GetPreviousClayTripForReturnedAsync(line.Id, ct);
            if (previousTrip == null)
            {
                throw new InvalidOperationException("Không có dữ liệu chuyến xe gần nhất trước đó của xe này. Vui lòng kiểm tra lại.");
            }

            resolution = ReturnedBrokenTripWeightLimiter.Resolve(actualReturnedWeight, previousTrip.NetWeightKg);
        }

        var now = _clock.NowLocal;
        line.IsReturnedBrokenTrip = isReturnedBrokenTrip;
        line.ActualAllocatedWeight = isReturnedBrokenTrip
            ? resolution!.RecognizedWeightKg
            : actualReturnedWeight;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TOGGLE_CLAY_RETURNED_TRIP",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(new
            {
                SessionNo = session.SessionNo,
                VehiclePlate = session.InternalVehicleNo ?? session.VehiclePlate,
                VesselName = cutOrder.VehiclePlate,
                OldActualAllocatedWeight = oldAllocatedWeight,
                ActualReturnedWeight = actualReturnedWeight,
                PreviousTripSessionId = previousTrip?.SessionId,
                PreviousTripSessionLineId = previousTrip?.SessionLineId,
                PreviousTripSessionNo = previousTrip?.SessionNo,
                PreviousTripWeight = previousTrip?.NetWeightKg,
                ReturnedRecognizedWeight = line.ActualAllocatedWeight,
                IsReturnedWeightCapped = resolution?.IsCapped ?? false,
                OldIsReturnedBrokenTrip = oldState,
                NewIsReturnedBrokenTrip = isReturnedBrokenTrip,
                NetWeight = session.NetWeight
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateLineAsync(line, innerCt);
            await _auditLogRepo.AddAsync(auditLog, innerCt);
        }, ct);
    }

    private static decimal ResolveActualAllocatedWeight(WeighingSession session, WeighingSessionLine line)
    {
        if (session.NetWeight.HasValue && session.NetWeight.Value > 0m)
        {
            return session.NetWeight.Value;
        }

        return Math.Max(0m, line.ActualAllocatedWeight ?? 0m);
    }
}