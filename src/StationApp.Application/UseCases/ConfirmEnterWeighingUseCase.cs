using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class ConfirmEnterWeighingUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;

    public ConfirmEnterWeighingUseCase(
        IVehicleRegistrationRepository regRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit)
    {
        _regRepo = regRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
    }

    public async Task ExecuteAsync(ConfirmEnterWeighingRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.RegistrationId, ct)
            ?? throw new Exception($"Vehicle Registration {request.RegistrationId} not found");

        if (reg.ProcessingStage != ProcessingStage.IN_YARD)
            throw new InvalidOperationException($"Xe không ở trạng thái IN_YARD, hiện tại: {reg.ProcessingStage}");

        if (reg.IsCancelled)
            throw new InvalidOperationException("Phiếu đã bị hủy, không thể chuyển vào cân.");

        var now = _clock.NowLocal;
        reg.ProcessingStage = ProcessingStage.WEIGHING;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.UpdateAsync(reg, innerCt);
        }, ct);

        await _audit.LogAsync("CONFIRM_ENTER_WEIGHING", nameof(Domain.Entities.VehicleRegistration), reg.Id,
            new { reg.VehiclePlate, reg.ProcessingStage }, ct);
    }
}
