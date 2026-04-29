using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class TryMoveToOutYardUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;

    public TryMoveToOutYardUseCase(
        IVehicleRegistrationRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit)
    {
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
    }

    public async Task<bool> ExecuteAsync(Guid registrationId, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(registrationId, ct);
        if (reg == null) return false;

        if (reg.ProcessingStage != ProcessingStage.WEIGHING) return false;
        if (reg.RegistrationStatus != RegistrationStatus.COMPLETED) return false;
        if (reg.IsCancelled) return false;

        var weighTickets = await _weighRepo.GetByVehicleRegistrationIdAsync(registrationId, ct);
        var activeWeigh = weighTickets
            .Where(wt => !wt.IsDeleted && string.Equals(wt.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activeWeigh.Count == 0 || !activeWeigh.All(wt => wt.IsPrinted))
            return false;

        var deliveryTickets = await _deliveryRepo.GetByVehicleRegistrationIdAsync(registrationId, ct);
        var activeDelivery = deliveryTickets
            .Where(dt => !dt.IsDeleted && string.Equals(dt.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activeDelivery.Count == 0 || !activeDelivery.All(dt => dt.IsPrinted))
            return false;

        var now = _clock.NowLocal;
        reg.ProcessingStage = ProcessingStage.OUT_YARD;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.UpdateAsync(reg, innerCt);
        }, ct);

        await _audit.LogAsync("AUTO_MOVE_TO_OUT_YARD", nameof(Domain.Entities.VehicleRegistration), reg.Id,
            new { reg.VehiclePlate, WeighPrinted = activeWeigh.Count, DeliveryPrinted = activeDelivery.Count }, ct);

        return true;
    }
}
