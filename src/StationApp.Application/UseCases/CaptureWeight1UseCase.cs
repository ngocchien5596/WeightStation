using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;

namespace StationApp.Application.UseCases;

public sealed class CaptureWeight1UseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CaptureWeight1UseCase(
        ICutOrderRepository regRepo,
        IWeighTicketRepository ticketRepo,
        ISyncOutboxRepository outboxRepo,
        IUnitOfWork uow,
        ITicketNumberGenerator ticketNoGen,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncPayloadFactory payloadFactory)
    {
        _regRepo = regRepo;
        _ticketRepo = ticketRepo;
        _outboxRepo = outboxRepo;
        _uow = uow;
        _ticketNoGen = ticketNoGen;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(CaptureWeightRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new Exception($"Cut order {request.CutOrderId} not found");

        // Section 15.2 Guard
        if (reg.CutOrderStatus != CutOrderStatus.REGISTERED &&
            reg.CutOrderStatus != CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException($"Cannot capture Weight 1 when status is {reg.CutOrderStatus}");
        }

        var now = _clock.NowLocal;
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = reg.Id,
            TicketNo = await _ticketNoGen.GenerateAsync(ct),
            ErpCutOrderId = reg.ErpCutOrderId,
            VehiclePlate = reg.VehiclePlate,
            MoocNumber = reg.MoocNumber,
            DriverName = reg.ReceiverName,
            CustomerCode = reg.CustomerCode,
            CustomerName = reg.CustomerName,
            ProductCode = reg.ProductCode,
            ProductName = reg.ProductName,
            PlannedWeight = reg.PlannedWeight,
            BagCount = reg.BagCount,
            Notes = reg.Notes,
            TransactionType = reg.TransactionType,
            TransportMethod = reg.TransportMethod,
            IsCancelled = false,
            Status = TicketStatus.LOADING_STARTED,
            RecordRole = "WORKING",
            IsPrimaryDisplay = true,

            Weight1 = request.Weight,
            Weight1User = _userContext.Username,
            Weight1Time = now,
            Weight1UpdatedAt = now,
            Weight1Mode = request.Mode,
            Weight1IsStable = request.IsStable,

            IdempotencyKey = Guid.NewGuid(),
            SyncStatus = SyncStatus.SYNC_QUEUED,
            AppVersion = _versionProvider.GetVersion(),
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        reg.CurrentPrimaryWeighTicketId = ticket.Id;
        reg.CutOrderStatus = CutOrderStatus.LOADING_IN_PROGRESS;
        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _ticketRepo.AddAsync(ticket, innerCt);
            await _regRepo.UpdateAsync(reg, innerCt);

            // Outbox for CutOrder
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = reg.Id,
                AggregateType = nameof(CutOrder),
                PayloadJson = _payloadFactory.CreatePayload(reg),
                IdempotencyKey = reg.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

            // Outbox for WeighTicket
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = ticket.Id,
                AggregateType = nameof(WeighTicket),
                PayloadJson = _payloadFactory.CreatePayload(ticket),
                IdempotencyKey = ticket.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);
        }, ct);

        await _audit.LogAsync("CAPTURE_WEIGHT_1", nameof(CutOrder), reg.Id,
            new { ticket.TicketNo, ticket.Weight1, ticket.Weight1IsStable, ticket.Weight1Mode }, ct);

        return OperationResult<WeighTicket>.Ok(ticket);
    }
}




