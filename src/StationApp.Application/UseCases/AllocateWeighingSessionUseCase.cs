using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Application.Formatting;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class AllocateWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly WeighingSessionOverweightService _overweightService;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public AllocateWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IDeliveryNumberGenerator deliveryNoGen,
        ITicketNumberGenerator ticketNoGen,
        WeighingSessionOverweightService overweightService,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _deliveryNoGen = deliveryNoGen;
        _ticketNoGen = ticketNoGen;
        _overweightService = overweightService;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(AllocateWeighingSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if ((session.SessionStatus != WeighingSessionStatus.ALLOCATION_PENDING
             && session.SessionStatus != WeighingSessionStatus.READY_TO_COMPLETE)
            || !session.NetWeight.HasValue)
        {
            throw new InvalidOperationException("Lượt cân hiện tại chưa sẵn sàng để phân bổ.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var sessionDeliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTicketByLineId = sessionDeliveryTickets
            .Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal)
            .Where(x => x.WeighingSessionLineId.HasValue)
            .ToDictionary(x => x.WeighingSessionLineId!.Value);
        var deliveryMasterTicket = sessionDeliveryTickets
            .FirstOrDefault(x => x.RecordRole == DeliveryTicketRecordRoles.Master && !x.IsDeleted);
        var lineWeighTicketsByCutOrderId = weighTickets
            .Where(x => x.RecordRole == WeighTicketRecordRoles.CutOrderDerived)
            .ToDictionary(x => x.CutOrderId);
        var inputByLineId = request.Lines.ToDictionary(x => x.SessionLineId);
        var inputOrderByLineId = request.Lines
            .Select((line, index) => new { line.SessionLineId, Index = index })
            .ToDictionary(x => x.SessionLineId, x => x.Index);
        var ticketsToCreate = new List<DeliveryTicket>();
        var weighTicketsToCreate = new List<WeighTicket>();
        var registrationById = registrations.ToDictionary(x => x.Id);

        var totalAllocated = 0m;
        foreach (var line in lines)
        {
            if (!inputByLineId.TryGetValue(line.Id, out var input))
            {
                throw new InvalidOperationException("Thiếu dữ liệu phân bổ cho một hoặc nhiều dòng.");
            }

            totalAllocated += input.ActualAllocatedWeight ?? 0m;
        }

        if (totalAllocated != session.NetWeight.Value)
        {
            throw new InvalidOperationException("Tổng khối lượng phân bổ phải đúng bằng khối lượng thực cân của lượt xe.");
        }

        var nextDeliveryNumbers = new Queue<string>(await AllocateDeliveryNumbersAsync(
            lines.Count(line => !deliveryTicketByLineId.ContainsKey(line.Id))
                + (lines.Count > 1 && deliveryMasterTicket == null ? 1 : 0),
            ct));
        var nextWeighTicketNumbers = new Queue<string>(await AllocateWeighTicketNumbersAsync(
            lines.Count(line =>
            {
                var registration = registrationById[line.CutOrderId];
                return !lineWeighTicketsByCutOrderId.ContainsKey(registration.Id);
            }),
            ct));

        var now = _clock.NowLocal;
        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);
        var lineTicketStartWeight = session.Weight1 ?? 0m;
        if (lines.Count > 1)
        {
            var primaryLine = lines.OrderBy(x => x.SequenceNo).First();
            var primaryRegistration = registrationById[primaryLine.CutOrderId];
            if (deliveryMasterTicket == null)
            {
                deliveryMasterTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = primaryRegistration.Id,
                    WeighingSessionId = session.Id,
                    DeliveryNo = nextDeliveryNumbers.Dequeue(),
                    ErpCutOrderId = primaryRegistration.ErpCutOrderId ?? string.Empty,
                    CustomerCode = primaryRegistration.CustomerCode,
                    ProductCode = primaryRegistration.ProductCode,
                    Notes = primaryRegistration.Notes,
                    RecordRole = DeliveryTicketRecordRoles.Master,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                };
                ticketsToCreate.Add(deliveryMasterTicket);
            }

            deliveryMasterTicket.AllocatedWeight = session.NetWeight;
            deliveryMasterTicket.AllocatedBagCount = request.Lines.Sum(x => x.ActualAllocatedBagCount ?? 0);
            deliveryMasterTicket.UpdatedAt = now;
            deliveryMasterTicket.UpdatedBy = _userContext.Username;
        }
        foreach (var line in lines.OrderBy(x => inputOrderByLineId.GetValueOrDefault(x.Id, int.MaxValue)))
        {
            var input = inputByLineId[line.Id];
            var registration = registrationById[line.CutOrderId];
            line.ActualAllocatedWeight = input.ActualAllocatedWeight;
            line.ActualAllocatedBagCount = WeighingSessionBagCountHelper.ResolveActualBagCount(
                registration.ProductType,
                registration.BagCount,
                line.PlannedBagCount,
                input.ActualAllocatedBagCount);
            line.BagCountDisplay = BagCountDisplayHelper.Resolve(
                input.ActualAllocatedWeight,
                registration.BagWeightKg,
                line.ActualAllocatedBagCount);
            line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;

            var deliveryTicket = deliveryTicketByLineId.GetValueOrDefault(line.Id);
            if (deliveryTicket == null)
            {
                deliveryTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = registration.Id,
                    WeighingSessionId = session.Id,
                    WeighingSessionLineId = line.Id,
                    DeliveryNo = nextDeliveryNumbers.Dequeue(),
                    ErpCutOrderId = registration.ErpCutOrderId ?? string.Empty,
                    CustomerCode = registration.CustomerCode,
                    ProductCode = registration.ProductCode,
                    Notes = registration.Notes,
                    RecordRole = DeliveryTicketRecordRoles.Normal,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                };
                ticketsToCreate.Add(deliveryTicket);
                deliveryTicketByLineId[line.Id] = deliveryTicket;
            }

            deliveryTicket.AllocatedWeight = input.ActualAllocatedWeight;
            deliveryTicket.AllocatedBagCount = line.ActualAllocatedBagCount;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
            line.DeliveryTicketId = deliveryTicket.Id;

            if (lines.Count > 1)
            {
                var lineWeighTicket = lineWeighTicketsByCutOrderId.GetValueOrDefault(registration.Id);
                if (lineWeighTicket == null)
                {
                    lineWeighTicket = new WeighTicket
                    {
                        Id = Guid.NewGuid(),
                        TicketNo = nextWeighTicketNumbers.Dequeue(),
                        IdempotencyKey = Guid.NewGuid(),
                        RecordRole = WeighTicketRecordRoles.CutOrderDerived,
                        CreatedAt = now,
                        CreatedBy = _userContext.Username,
                        Weight1User = session.Weight1Time.HasValue ? _userContext.Username : null,
                        Weight1UpdatedAt = session.Weight1Time.HasValue ? now : null,
                        Weight2User = session.Weight2Time.HasValue ? _userContext.Username : null,
                        Weight2UpdatedAt = session.Weight2Time.HasValue ? now : null
                    };
                    weighTicketsToCreate.Add(lineWeighTicket);
                    lineWeighTicketsByCutOrderId[registration.Id] = lineWeighTicket;
                }

                _ticketSyncService.SyncLineTicketFromSession(session, line, registration, lineWeighTicket, lineTicketStartWeight, now, _userContext.Username);
                if (masterWeighTicket != null)
                {
                    lineWeighTicket.VehicleRegistrationNoSnapshot = masterWeighTicket.VehicleRegistrationNoSnapshot;
                    lineWeighTicket.VehicleRegistrationExpirySnapshot = masterWeighTicket.VehicleRegistrationExpirySnapshot;
                    lineWeighTicket.MoocRegistrationNoSnapshot = masterWeighTicket.MoocRegistrationNoSnapshot;
                    lineWeighTicket.MoocRegistrationExpirySnapshot = masterWeighTicket.MoocRegistrationExpirySnapshot;
                    lineWeighTicket.Weight1Mode = masterWeighTicket.Weight1Mode;
                    lineWeighTicket.Weight1IsStable = masterWeighTicket.Weight1IsStable;
                    lineWeighTicket.Weight2Mode = masterWeighTicket.Weight2Mode;
                    lineWeighTicket.Weight2IsStable = masterWeighTicket.Weight2IsStable;
                }

                registration.CurrentPrimaryWeighTicketId = lineWeighTicket.Id;
                registration.UpdatedAt = now;
                registration.UpdatedBy = _userContext.Username;
                lineTicketStartWeight = lineWeighTicket.Weight2 ?? lineTicketStartWeight;
            }
            else
            {
                registration.CurrentPrimaryWeighTicketId = masterWeighTicket?.Id;
            }
        }

        _overweightService.RefreshSessionOverweightState(
            session,
            lines,
            weighTickets,
            sessionDeliveryTickets,
            now,
            _userContext.Username);

        foreach (var deliveryTicket in deliveryTicketByLineId.Values)
        {
            deliveryTicket.IsOverWeight = session.IsOverweight;
        }

        if (lines.Count == 1)
        {
            if (deliveryMasterTicket != null && !deliveryMasterTicket.IsDeleted)
            {
                deliveryMasterTicket.IsDeleted = true;
                deliveryMasterTicket.DeletedAt = now;
                deliveryMasterTicket.DeletedBy = _userContext.Username;
                deliveryMasterTicket.UpdatedAt = now;
                deliveryMasterTicket.UpdatedBy = _userContext.Username;
            }

            foreach (var extraWeighTicket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.CutOrderDerived && !x.IsDeleted))
            {
                extraWeighTicket.IsDeleted = true;
                extraWeighTicket.DeletedAt = now;
                extraWeighTicket.DeletedBy = _userContext.Username;
                extraWeighTicket.UpdatedAt = now;
                extraWeighTicket.UpdatedBy = _userContext.Username;
            }
        }

        if (masterWeighTicket != null)
        {
            _ticketSyncService.SyncMasterTicketFromSession(session, masterWeighTicket, now, _userContext.Username);
        }

        session.SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var ticket in ticketsToCreate)
            {
                await _deliveryRepo.AddAsync(ticket, innerCt);
            }

            foreach (var ticket in weighTicketsToCreate)
            {
                await _weighRepo.AddAsync(ticket, innerCt);
            }

            foreach (var ticket in deliveryTicketByLineId.Values)
            {
                if (!ticketsToCreate.Contains(ticket))
                {
                    await _deliveryRepo.UpdateAsync(ticket, innerCt);
                }
            }

            if (deliveryMasterTicket != null && !ticketsToCreate.Contains(deliveryMasterTicket))
            {
                await _deliveryRepo.UpdateAsync(deliveryMasterTicket, innerCt);
            }

            foreach (var ticket in lineWeighTicketsByCutOrderId.Values)
            {
                if (!weighTicketsToCreate.Contains(ticket))
                {
                    await _weighRepo.UpdateAsync(ticket, innerCt);
                }
            }

            foreach (var ticket in sessionDeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived))
            {
                await _deliveryRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession
                                                        || x.RecordRole == WeighTicketRecordRoles.CutOrderDerived
                                                        || x.RecordRole == WeighTicketRecordRoles.SplitDerived))
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            await _sessionRepo.UpdateAsync(session, innerCt);
        }, ct);
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

    private async Task<IReadOnlyList<string>> AllocateWeighTicketNumbersAsync(int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        if (count == 1)
        {
            return [await _ticketNoGen.GenerateAsync(ct)];
        }

        var numbers = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            numbers.Add(await _ticketNoGen.GenerateAsync(ct));
        }

        return numbers;
    }
}
