using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases.MasterData;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class UpdateIncomingRegistrationUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IErpCutOrderWriteBackService _erpCutOrderWriteBackService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly EnsureInboundMasterDataUseCase _ensureInboundMasterDataUseCase;

    public UpdateIncomingRegistrationUseCase(
        ICutOrderRepository regRepo,
        IErpCutOrderWriteBackService erpCutOrderWriteBackService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        EnsureInboundMasterDataUseCase ensureInboundMasterDataUseCase)
    {
        _regRepo = regRepo;
        _erpCutOrderWriteBackService = erpCutOrderWriteBackService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _ensureInboundMasterDataUseCase = ensureInboundMasterDataUseCase;
    }

    public async Task<OperationResult<CutOrder>> ExecuteAsync(UpdateIncomingRegistrationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VehiclePlate))
        {
            return OperationResult<CutOrder>.Fail("Số PTVC không được để trống.");
        }

        var reg = await _regRepo.GetByIdAsync(request.CutOrderId, ct);
        if (reg == null)
        {
            return OperationResult<CutOrder>.Fail("Không tìm thấy phiếu xe vào.");
        }

        if (reg.ProcessingStage != ProcessingStage.IN_YARD)
        {
            return OperationResult<CutOrder>.Fail("Chỉ được sửa phiếu đang ở Danh sách xe vào.");
        }

        reg.TransactionType = request.TransactionType;
        reg.TransportMethod = request.TransportMethod;
        reg.VehiclePlate = request.VehiclePlate.Trim();
        reg.MoocNumber = request.MoocNumber?.Trim();
        reg.ReceiverName = request.ReceiverName?.Trim();
        reg.CustomerCode = request.CustomerCode?.Trim();
        reg.CustomerName = request.CustomerName?.Trim();
        reg.ProductCode = request.ProductCode?.Trim();
        reg.ProductName = request.ProductName?.Trim();
        reg.ProductType = ProductTypes.Normalize(request.ProductType) ?? ProductTypes.InferForTransaction(request.TransactionType);
        reg.PlannedWeight = request.PlannedWeight;
        reg.BagCount = request.BagCount;
        reg.Notes = request.Notes?.Trim();
        reg.IsCancelled = request.IsCancelled;
        reg.UpdatedAt = _clock.NowLocal;
        reg.UpdatedBy = _userContext.Username;

        if (ShouldWriteBackToErp(reg))
        {
            if (string.IsNullOrWhiteSpace(reg.ErpCutOrderId))
            {
                return OperationResult<CutOrder>.Fail("Không tìm thấy mã cắt lệnh ERP để cập nhật Số PTVC/Mooc.");
            }

            try
            {
                var normalizedErpCutOrderId = reg.ErpCutOrderId.Trim();
                var updatedAt = reg.UpdatedAt ?? _clock.NowLocal;

                var erpResult = await _erpCutOrderWriteBackService.UpdateTransportInfoAsync(
                    new ErpCutOrderWriteBackRequest(
                        normalizedErpCutOrderId,
                        reg.VehiclePlate,
                        reg.MoocNumber,
                        _userContext.Username,
                        updatedAt),
                    ct);

                if (erpResult.AffectedRows <= 0)
                {
                    return OperationResult<CutOrder>.Fail($"ERP không tìm thấy cắt lệnh {reg.ErpCutOrderId} để cập nhật.");
                }

                var noteResult = await _erpCutOrderWriteBackService.UpdateDescriptionAsync(
                    new ErpCutOrderNoteWriteBackRequest(
                        normalizedErpCutOrderId,
                        reg.Notes,
                        _userContext.Username,
                        updatedAt),
                    ct);

                if (noteResult.AffectedRows <= 0)
                {
                    return OperationResult<CutOrder>.Fail($"ERP không tìm thấy cắt lệnh {reg.ErpCutOrderId} để cập nhật ghi chú.");
                }

                var receiverResult = await _erpCutOrderWriteBackService.UpdateReceiverAsync(
                    new ErpCutOrderReceiverWriteBackRequest(
                        normalizedErpCutOrderId,
                        reg.ReceiverName,
                        _userContext.Username,
                        updatedAt),
                    ct);

                if (receiverResult.AffectedRows <= 0)
                {
                    return OperationResult<CutOrder>.Fail($"ERP không tìm thấy cắt lệnh {reg.ErpCutOrderId} để cập nhật tên tài xế.");
                }
            }
            catch (Exception ex)
            {
                return OperationResult<CutOrder>.Fail($"Không thể cập nhật ERP cho cắt lệnh {reg.ErpCutOrderId}: {ex.Message}");
            }
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.UpdateAsync(reg, innerCt);
            await _ensureInboundMasterDataUseCase.ExecuteAsync(
                reg.VehiclePlate,
                reg.MoocNumber,
                reg.ReceiverName,
                reg.TransportMethod,
                reg.CustomerCode,
                reg.CustomerName,
                reg.ProductCode,
                reg.ProductName,
                reg.ProductType,
                reg.TransactionType,
                innerCt,
                request.TtcpWeight,
                request.VehicleRegistrationNo,
                request.VehicleRegistrationExpiryDate,
                request.MoocRegistrationNo,
                request.MoocRegistrationExpiryDate);
        }, ct);

        await _audit.LogAsync(
            "UPDATE_INCOMING_REGISTRATION",
            nameof(CutOrder),
            reg.Id,
            new { reg.VehiclePlate, reg.TransactionType, reg.IsCancelled },
            ct);

        return OperationResult<CutOrder>.Ok(reg);
    }

    private static bool ShouldWriteBackToErp(CutOrder reg)
    {
        return reg.CutOrderSource == CutOrderSource.ERP
            && !string.IsNullOrWhiteSpace(reg.ErpCutOrderId);
    }
}


