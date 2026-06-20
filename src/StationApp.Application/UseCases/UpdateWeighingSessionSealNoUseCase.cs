using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class UpdateWeighingSessionSealNoUseCase
{
    private readonly ICutOrderRepository _cutOrderRepository;
    private readonly IErpCutOrderWriteBackService _erpCutOrderWriteBackService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IClock _clock;
    private readonly IAuditService _auditService;

    public UpdateWeighingSessionSealNoUseCase(
        ICutOrderRepository cutOrderRepository,
        IErpCutOrderWriteBackService erpCutOrderWriteBackService,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IClock clock,
        IAuditService auditService)
    {
        _cutOrderRepository = cutOrderRepository;
        _erpCutOrderWriteBackService = erpCutOrderWriteBackService;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _clock = clock;
        _auditService = auditService;
    }

    public async Task<OperationResult<string?>> ExecuteAsync(Guid weighingSessionId, string? sealNo, CancellationToken ct)
    {
        var registrations = await _cutOrderRepository.GetByWeighingSessionIdAsync(weighingSessionId, ct);
        if (registrations.Count == 0)
        {
            return OperationResult<string?>.Fail("Không tìm thấy cắt lệnh cho lượt cân để cập nhật niêm chì số.");
        }

        var normalizedSealNo = NormalizeOptional(sealNo);
        var now = _clock.NowLocal;
        var erpRegistrations = registrations
            .Where(ShouldWriteBackToErp)
            .ToList();

        foreach (var registration in erpRegistrations)
        {
            try
            {
                var result = await _erpCutOrderWriteBackService.UpdateSealNoAsync(
                    new ErpCutOrderSealWriteBackRequest(
                        registration.ErpCutOrderId!.Trim(),
                        normalizedSealNo,
                        _currentUserContext.Username,
                        now),
                    ct);

                if (result.AffectedRows <= 0)
                {
                    return OperationResult<string?>.Fail($"ERP không tìm thấy cắt lệnh {registration.ErpCutOrderId} để cập nhật niêm chì số.");
                }
            }
            catch (Exception ex)
            {
                return OperationResult<string?>.Fail($"Không thể cập nhật ERP cho cắt lệnh {registration.ErpCutOrderId}: {ex.Message}");
            }
        }

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var registration in registrations)
            {
                registration.SealNo = normalizedSealNo;
                registration.UpdatedAt = now;
                registration.UpdatedBy = _currentUserContext.Username;
                await _cutOrderRepository.UpdateAsync(registration, innerCt);
            }
        }, ct);

        await _auditService.LogAsync(
            "UPDATE_WEIGHING_SESSION_SEAL_NO",
            nameof(CutOrder),
            registrations[0].Id,
            new
            {
                WeighingSessionId = weighingSessionId,
                SealNo = normalizedSealNo,
                UpdatedCutOrderIds = registrations.Select(x => x.Id).ToArray(),
                UpdatedErpCutOrderIds = erpRegistrations.Select(x => x.ErpCutOrderId).ToArray()
            },
            ct);

        return OperationResult<string?>.Ok(normalizedSealNo);
    }

    private static bool ShouldWriteBackToErp(CutOrder registration)
    {
        return registration.CutOrderSource == CutOrderSource.ERP
            && !string.IsNullOrWhiteSpace(registration.ErpCutOrderId);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
