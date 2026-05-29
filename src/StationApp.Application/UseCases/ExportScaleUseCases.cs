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

        var existingTrips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        if (existingTrips.Any(x => x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1
                or WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111ang c\u00f3 chuy\u1ebfn xe ch\u01b0a ho\u00e0n t\u1ea5t c\u00e2n/ph\u00e2n b\u1ed5.");
        }

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
