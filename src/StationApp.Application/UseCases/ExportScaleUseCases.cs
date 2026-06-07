using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class TransitionToExportScaleUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public TransitionToExportScaleUseCase(
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

    public async Task ExecuteAsync(TransitionToExportScaleRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Ch\u1ec9 h\u1ed7 tr\u1ee3 c\u00e2n xu\u1ea5t kh\u1ea9u cho c\u1eaft l\u1ec7nh xu\u1ea5t h\u00e0ng.");
        }

        if (cutOrder.IsExportScale)
        {
            return;
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.REGISTERED || cutOrder.ProcessingStage != ProcessingStage.IN_YARD)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng c\u00f2n \u1edf tr\u1ea1ng th\u00e1i xe v\u00e0o \u0111\u1ec3 chuy\u1ec3n sang c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (cutOrder.WeighingSessionId.HasValue)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 thu\u1ed9c m\u1ed9t l\u01b0\u1ee3t c\u00e2n kh\u00e1c.");
        }

        var now = _clock.NowLocal;
        cutOrder.IsExportScale = true;
        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.ExportStartedAt = now;
        cutOrder.ExportStartedBy = _userContext.Username;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}

public sealed class CreateExportVehicleSessionUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighingSessionNumberGenerator _sessionNoGen;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateExportVehicleSessionUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        IWeighingSessionNumberGenerator sessionNoGen,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _sessionNoGen = sessionNoGen;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<CreateExportVehicleSessionResult> ExecuteAsync(CreateExportVehicleSessionRequest request, CancellationToken ct)
    {
        var vehiclePlate = request.VehiclePlate?.Trim();
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            throw new InvalidOperationException("Vui l\u00f2ng nh\u1eadp bi\u1ec3n s\u1ed1 xe cho chuy\u1ebfn xu\u1ea5t kh\u1ea9u.");
        }

        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        ValidateOpenExportCutOrder(cutOrder);

        var plannedWeightForTrip = await ResolveRemainingPlannedWeightAsync(cutOrder, ct);
        var now = _clock.NowLocal;
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = await _sessionNoGen.GenerateAsync(TransactionType.OUTBOUND, ct),
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = vehiclePlate,
            MoocNumber = NormalizeOptional(request.MoocNumber),
            DriverName = NormalizeOptional(request.DriverName),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
            OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
            OverweightAmount = 0m,
            IsCancelled = false,
            HasPrintedMasterWeighTicket = false,
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
            DistributorName = cutOrder.CustomerName,
            ProductCode = cutOrder.ProductCode,
            ProductName = cutOrder.ProductName,
            PlannedWeight = plannedWeightForTrip,
            PlannedBagCount = cutOrder.BagCount,
            LineStatus = WeighingSessionLineStatus.PENDING,
            HasPrintedDeliveryTicket = false,
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        cutOrder.WeighingSessionId = null;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await UpsertVehicleMasterAsync(request, vehiclePlate, now, innerCt);
            await _sessionRepo.AddAsync(session, innerCt);
            await _sessionRepo.AddLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
        }, ct);

        return new CreateExportVehicleSessionResult(session.Id, session.SessionNo);
    }

    private async Task UpsertVehicleMasterAsync(
        CreateExportVehicleSessionRequest request,
        string vehiclePlate,
        DateTime now,
        CancellationToken ct)
    {
        var moocNumber = NormalizeOptional(request.MoocNumber);
        Vehicle? vehicle = null;

        if (!string.IsNullOrWhiteSpace(moocNumber))
        {
            vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(vehiclePlate, moocNumber, ct);
        }

        var samePlateVehicles = await _vehicleRepo.GetByPlateAsync(vehiclePlate, ct);
        vehicle ??= string.IsNullOrWhiteSpace(moocNumber)
            ? samePlateVehicles.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.MoocNumber)) ?? samePlateVehicles.FirstOrDefault()
            : samePlateVehicles.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.MoocNumber));

        if (vehicle == null)
        {
            vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = vehiclePlate,
                MoocNumber = moocNumber ?? string.Empty,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            ApplyVehicleMasterPayload(vehicle, request, moocNumber);
            await _vehicleRepo.AddAsync(vehicle, ct);
            return;
        }

        vehicle.VehiclePlate = vehiclePlate;
        vehicle.MoocNumber = moocNumber ?? string.Empty;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = _userContext.Username;
        ApplyVehicleMasterPayload(vehicle, request, moocNumber);
        await _vehicleRepo.UpdateAsync(vehicle, ct);
    }

    private static void ApplyVehicleMasterPayload(
        Vehicle vehicle,
        CreateExportVehicleSessionRequest request,
        string? moocNumber)
    {
        vehicle.DriverName = NormalizeOptional(request.DriverName);
        vehicle.TtcpWeight = request.TtcpWeight;
        vehicle.VehicleRegistrationNo = NormalizeOptional(request.VehicleRegistrationNo);
        vehicle.VehicleRegistrationExpiryDate = request.VehicleRegistrationExpiryDate;
        vehicle.MoocNumber = moocNumber ?? string.Empty;
        vehicle.MoocRegistrationNo = NormalizeOptional(request.MoocRegistrationNo);
        vehicle.MoocRegistrationExpiryDate = request.MoocRegistrationExpiryDate;
    }

    private async Task<decimal?> ResolveRemainingPlannedWeightAsync(CutOrder cutOrder, CancellationToken ct)
    {
        var activeSummary = await _cutOrderRepo.GetActiveExportScaleCutOrdersAsync(
            new ExportScaleCutOrderFilter(cutOrder.ErpCutOrderId, null, null, null, null),
            ct);
        var currentSummary = activeSummary.FirstOrDefault(x => x.CutOrderId == cutOrder.Id);
        return currentSummary != null && currentSummary.RemainingWeight > 0m
            ? currentSummary.RemainingWeight
            : cutOrder.PlannedWeight;
    }

    private static void ValidateOpenExportCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh ch\u01b0a \u0111\u01b0\u1ee3c chuy\u1ec3n sang lu\u1ed3ng c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Ch\u1ec9 h\u1ed7 tr\u1ee3 c\u00e2n xu\u1ea5t kh\u1ea9u cho c\u1eaft l\u1ec7nh xu\u1ea5t h\u00e0ng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 ch\u1ed1t, kh\u00f4ng th\u1ec3 t\u1ea1o th\u00eam chuy\u1ebfn xe.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng \u1edf tr\u1ea1ng th\u00e1i c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TransferExportVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public TransferExportVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(TransferExportVehicleTripRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe cần chuyển.");

        if (session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến xe đã bị hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var activeLines = lines.Where(x => !x.IsDeleted).ToList();
        if (activeLines.Count != 1)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến xe xuất khẩu có đúng 1 dòng cắt lệnh.");
        }

        var line = activeLines[0];
        var sourceCutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh nguồn của chuyến xe.");
        var targetCutOrder = await _cutOrderRepo.GetByIdAsync(request.TargetCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh đích.");

        ValidateTransferSourceCutOrder(sourceCutOrder);
        ValidateTransferTargetCutOrder(targetCutOrder);

        if (sourceCutOrder.Id == targetCutOrder.Id)
        {
            return;
        }

        var now = _clock.NowLocal;
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var sourceExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var sourceExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var targetExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetPlannedWeight = await ResolveRemainingPlannedWeightAsync(targetCutOrder, ct);

        line.CutOrderId = targetCutOrder.Id;
        line.CustomerCode = targetCutOrder.CustomerCode;
        line.CustomerName = targetCutOrder.CustomerName;
        line.DistributorCode = targetCutOrder.CustomerCode;
        line.DistributorName = targetCutOrder.CustomerName;
        line.ProductCode = targetCutOrder.ProductCode;
        line.ProductName = targetCutOrder.ProductName;
        line.PlannedWeight = targetPlannedWeight;
        line.PlannedBagCount = targetCutOrder.BagCount;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        foreach (var weighTicket in weighTickets.Where(x => !x.IsDeleted))
        {
            weighTicket.CutOrderId = targetCutOrder.Id;
            weighTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId;
            weighTicket.CustomerCode = targetCutOrder.CustomerCode;
            weighTicket.CustomerName = targetCutOrder.CustomerName;
            weighTicket.ProductCode = targetCutOrder.ProductCode;
            weighTicket.ProductName = targetCutOrder.ProductName;
            weighTicket.PlannedWeight = targetPlannedWeight;
            weighTicket.BagCount = targetCutOrder.BagCount;
            weighTicket.Notes = targetCutOrder.Notes;
            weighTicket.TransportMethod = targetCutOrder.TransportMethod;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets.Where(x => !x.IsDeleted))
        {
            deliveryTicket.CutOrderId = targetCutOrder.Id;
            deliveryTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId ?? string.Empty;
            deliveryTicket.CustomerCode = targetCutOrder.CustomerCode;
            deliveryTicket.ProductCode = targetCutOrder.ProductCode;
            deliveryTicket.Notes = targetCutOrder.Notes;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        var sourceCurrentPrimaryWeighTicketId = sourceCutOrder.CurrentPrimaryWeighTicketId;
        var sourceCurrentPrimaryDeliveryTicketId = sourceCutOrder.CurrentPrimaryDeliveryTicketId;

        sourceCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        sourceCutOrder.UpdatedAt = now;
        sourceCutOrder.UpdatedBy = _userContext.Username;

        targetCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        targetCutOrder.UpdatedAt = now;
        targetCutOrder.UpdatedBy = _userContext.Username;
        targetCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(targetExistingWeighTickets.Concat(weighTickets))?.Id;
        targetCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(targetExistingDeliveryTickets.Concat(deliveryTickets))?.Id;

        if (sourceCurrentPrimaryWeighTicketId.HasValue && weighTickets.Any(x => x.Id == sourceCurrentPrimaryWeighTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(
                sourceExistingWeighTickets.Where(x => weighTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        if (sourceCurrentPrimaryDeliveryTicketId.HasValue && deliveryTickets.Any(x => x.Id == sourceCurrentPrimaryDeliveryTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(
                sourceExistingDeliveryTickets.Where(x => deliveryTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateLineAsync(line, innerCt);

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(sourceCutOrder, innerCt);
            await _cutOrderRepo.UpdateAsync(targetCutOrder, innerCt);
        }, ct);
    }

    private async Task<decimal?> ResolveRemainingPlannedWeightAsync(CutOrder cutOrder, CancellationToken ct)
    {
        var activeSummary = await _cutOrderRepo.GetActiveExportScaleCutOrdersAsync(
            new ExportScaleCutOrderFilter(cutOrder.ErpCutOrderId, null, null, null, null),
            ct);
        var currentSummary = activeSummary.FirstOrDefault(x => x.CutOrderId == cutOrder.Id);
        return currentSummary != null && currentSummary.RemainingWeight > 0m
            ? currentSummary.RemainingWeight
            : cutOrder.PlannedWeight;
    }

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static void ValidateTransferSourceCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn không thuộc luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến từ cắt lệnh đã chốt.");
        }
    }

    private static void ValidateTransferTargetCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh đích chưa được chuyển sang luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đích đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến sang cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến sang cắt lệnh đã chốt.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh đích không ở trạng thái cân xuất khẩu.");
        }
    }
}

public sealed class FinalizeExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public FinalizeExportCutOrderUseCase(
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

    public async Task ExecuteAsync(FinalizeExportCutOrderRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng thu\u1ed9c lu\u1ed3ng c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        if (trips.Any(x => x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1
                or WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("Kh\u00f4ng th\u1ec3 ch\u1ed1t khi c\u00f2n chuy\u1ebfn xe d\u1edf dang.");
        }

        var totalWeight = trips
            .Where(x => x.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED)
            .Sum(x => x.ActualAllocatedWeight ?? 0m);
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("Ch\u01b0a c\u00f3 chuy\u1ebfn xe h\u1ee3p l\u1ec7 \u0111\u1ec3 ch\u1ed1t s\u1ed1 l\u01b0\u1ee3ng.");
        }

        var now = _clock.NowLocal;
        cutOrder.ExportFinalizedWeight = totalWeight;
        cutOrder.ExportFinalizedAt = now;
        cutOrder.ExportFinalizedBy = _userContext.Username;
        cutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        cutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}
