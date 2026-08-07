using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

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
        var vehiclePlate = VehicleIdentifierNormalizer.NormalizePlate(request.VehiclePlate);
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            throw new InvalidOperationException("Vui lòng nhập biển số xe cho chuyến xuất khẩu.");
        }

        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh.");

        NormalizeRecoverableExportCutOrderState(cutOrder);
        ValidateOpenExportCutOrder(cutOrder);

        CreateExportVehicleSessionResult? result = null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.NowLocal;
            var sessionNo = await _sessionNoGen.GenerateAsync(TransactionType.OUTBOUND, innerCt);
            var plannedWeightForTrip = await ResolveRemainingPlannedWeightAsync(cutOrder, innerCt);

            var session = new WeighingSession
            {
                Id = Guid.NewGuid(),
                SessionNo = sessionNo,
                StationCode = cutOrder.StationCode,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = vehiclePlate,
                MoocNumber = VehicleIdentifierNormalizer.NormalizeOptional(request.MoocNumber),
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
                StationCode = cutOrder.StationCode,
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

            await UpsertVehicleMasterAsync(request, vehiclePlate, now, innerCt);
            await _sessionRepo.AddAsync(session, innerCt);
            await _sessionRepo.AddLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);

            result = new CreateExportVehicleSessionResult(session.Id, session.SessionNo);
        }, ct);

        return result!;
    }

    private async Task UpsertVehicleMasterAsync(
        CreateExportVehicleSessionRequest request,
        string vehiclePlate,
        DateTime now,
        CancellationToken ct)
    {
        var moocNumber = VehicleIdentifierNormalizer.NormalizeOptional(request.MoocNumber);
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
        if (request.TtcpWeight is > 0m)
        {
            vehicle.TtcpWeight = request.TtcpWeight;
        }

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
            throw new InvalidOperationException("Cắt lệnh chưa được chuyển sang luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ cân xuất khẩu cho cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cắt lệnh đã chốt, không thể tạo thêm chuyến xe.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh không ở trạng thái cân xuất khẩu.");
        }
    }

    private static void NormalizeRecoverableExportCutOrderState(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale
            || cutOrder.TransactionType != TransactionType.OUTBOUND
            || cutOrder.IsDeleted
            || cutOrder.IsCancelled
            || cutOrder.ExportFinalizedAt.HasValue
            || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var recoverableStatus = cutOrder.CutOrderStatus is CutOrderStatus.REGISTERED or CutOrderStatus.IN_SESSION;
        var recoverableStage = cutOrder.ProcessingStage is ProcessingStage.IN_YARD or ProcessingStage.WEIGHING;
        if (!recoverableStatus || !recoverableStage)
        {
            return;
        }

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
