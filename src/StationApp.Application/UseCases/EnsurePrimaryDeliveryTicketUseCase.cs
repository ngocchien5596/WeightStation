using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class EnsurePrimaryDeliveryTicketUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighTicketRepo;
    private readonly IDeliveryTicketRepository _deliveryTicketRepo;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public EnsurePrimaryDeliveryTicketUseCase(
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighTicketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        IDeliveryNumberGenerator deliveryNoGen,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _regRepo = regRepo;
        _weighTicketRepo = weighTicketRepo;
        _deliveryTicketRepo = deliveryTicketRepo;
        _deliveryNoGen = deliveryNoGen;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<DeliveryTicket?> ExecuteAsync(Guid registrationId, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(registrationId, ct)
            ?? throw new InvalidOperationException($"Cut order {registrationId} not found");

        var weighTickets = await _weighTicketRepo.GetByCutOrderIdAsync(registrationId, ct);
        var workingWeighTickets = weighTickets
            .Where(t => string.Equals(t.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.SplitSequence ?? 0)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var deliveryTickets = (await _deliveryTicketRepo.GetByCutOrderIdAsync(registrationId, ct))
            .Where(t => string.Equals(t.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.SplitSequence ?? 0)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        if (workingWeighTickets.Count == 0)
        {
            var existingPrimary = deliveryTickets.FirstOrDefault();
            if (existingPrimary != null && (!reg.CurrentPrimaryDeliveryTicketId.HasValue || reg.CurrentPrimaryDeliveryTicketId != existingPrimary.Id))
            {
                reg.CurrentPrimaryDeliveryTicketId = existingPrimary.Id;
                reg.UpdatedAt = _clock.NowLocal;
                reg.UpdatedBy = _userContext.Username;

                await _uow.ExecuteInTransactionAsync(async innerCt =>
                {
                    await _regRepo.UpdateAsync(reg, innerCt);
                }, ct);
            }

            return existingPrimary;
        }

        if (reg.CutOrderStatus != CutOrderStatus.COMPLETED)
        {
            return deliveryTickets.FirstOrDefault();
        }

        var now = _clock.NowLocal;
        var existingBySequence = deliveryTickets.ToDictionary(t => t.SplitSequence ?? 0, t => t);
        var ticketsToCreate = new List<DeliveryTicket>();
        Queue<string>? allocatedDeliveryNumbers = null;

        DeliveryTicket? primaryDeliveryTicket = null;
        DeliveryTicket? sourceDeliveryTicket = null;

        for (var index = 0; index < workingWeighTickets.Count; index++)
        {
            var weighTicket = workingWeighTickets[index];
            var sequence = weighTicket.SplitSequence ?? (byte)(index + 1);

            if (!existingBySequence.TryGetValue(sequence, out var deliveryTicket))
            {
                allocatedDeliveryNumbers ??= new Queue<string>(await AllocateDeliveryNumbersAsync(
                    workingWeighTickets.Count - deliveryTickets.Count,
                    ct));

                deliveryTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = reg.Id,
                    DeliveryNo = allocatedDeliveryNumbers.Dequeue(),
                    ErpCutOrderId = reg.ErpCutOrderId ?? string.Empty,
                    CustomerCode = reg.CustomerCode,
                    ProductCode = reg.ProductCode,
                    Notes = reg.Notes,
                    RecordRole = "WORKING",
                    SplitGroupId = weighTicket.SplitGroupId,
                    SplitSequence = sequence,
                    SourceDeliveryTicketId = sequence > 1 ? sourceDeliveryTicket?.Id : null,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                };

                existingBySequence[sequence] = deliveryTicket;
                ticketsToCreate.Add(deliveryTicket);
            }

            deliveryTicket.CutOrderId = reg.Id;
            deliveryTicket.ErpCutOrderId = reg.ErpCutOrderId ?? string.Empty;
            deliveryTicket.CustomerCode = reg.CustomerCode;
            deliveryTicket.ProductCode = reg.ProductCode;
            deliveryTicket.Notes = reg.Notes;
            deliveryTicket.RecordRole = "WORKING";
            deliveryTicket.SplitGroupId = weighTicket.SplitGroupId;
            deliveryTicket.SplitSequence = sequence;
            deliveryTicket.IsOverWeight = weighTicket.IsOverWeight;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;

            if (sequence == 1)
            {
                primaryDeliveryTicket = deliveryTicket;
                sourceDeliveryTicket ??= deliveryTicket;
            }
        }

        primaryDeliveryTicket ??= existingBySequence
            .OrderBy(kvp => kvp.Key == 0 ? int.MaxValue : kvp.Key)
            .Select(kvp => kvp.Value)
            .FirstOrDefault();

        if (primaryDeliveryTicket == null)
        {
            return null;
        }

        foreach (var ticket in ticketsToCreate.Where(t => (t.SplitSequence ?? 0) > 1 && t.SourceDeliveryTicketId == null))
        {
            ticket.SourceDeliveryTicketId = primaryDeliveryTicket.Id;
        }

        reg.CurrentPrimaryDeliveryTicketId = primaryDeliveryTicket.Id;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var createdTicket in ticketsToCreate)
            {
                await _deliveryTicketRepo.AddAsync(createdTicket, innerCt);
            }

            await _regRepo.UpdateAsync(reg, innerCt);
        }, ct);

        return primaryDeliveryTicket;
    }

    private async Task<IReadOnlyList<string>> AllocateDeliveryNumbersAsync(int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        if (count == 1)
        {
            return [await _deliveryNoGen.GenerateAsync(ct)];
        }

        var numbers = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            numbers.Add(await _deliveryNoGen.GenerateAsync(ct));
        }

        return numbers;
    }
}



