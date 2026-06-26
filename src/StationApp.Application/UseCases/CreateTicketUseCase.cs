using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateTicketUseCase
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CreateTicketUseCase(
        ITicketRepository ticketRepo,
        ISyncOutboxRepository outboxRepo,
        IUnitOfWork uow,
        ITicketNumberGenerator ticketNoGen,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncPayloadFactory payloadFactory)
    {
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

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(CreateTicketRequest request, CancellationToken ct)
    {
        WeighTicket ticket = null!;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var ticketNo = await _ticketNoGen.GenerateAsync(innerCt);
            var now = _clock.NowLocal;

            ticket = new WeighTicket
            {
                Id = Guid.NewGuid(),
                TicketNo = ticketNo,
                ErpCutOrderId = request.ErpCutOrderId,
                VehiclePlate = request.VehiclePlate,
                MoocNumber = request.MoocNumber,
                DriverName = request.DriverName,
                CustomerCode = request.CustomerCode,
                CustomerName = request.CustomerName,
                ProductCode = request.ProductCode,
                ProductName = request.ProductName,
                PlannedWeight = request.PlannedWeight,
                BagCount = request.BagCount,
                Notes = request.Notes,
                TransactionType = request.TransactionType,
                TransportMethod = request.TransportMethod,
                IsCancelled = false,
                Status = TicketStatus.TICKET_CREATED,
                IdempotencyKey = Guid.NewGuid(),
                SyncStatus = SyncStatus.SYNC_QUEUED,
                AppVersion = _versionProvider.GetVersion(),
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            await _ticketRepo.AddAsync(ticket, innerCt);

            var outbox = new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = ticket.Id,
                AggregateType = nameof(WeighTicket),
                PayloadJson = _payloadFactory.CreatePayload(ticket),
                IdempotencyKey = ticket.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now
            };
            await _outboxRepo.EnqueueAsync(outbox, innerCt);
        }, ct);

        await _audit.LogAsync("CREATE_TICKET", nameof(WeighTicket), ticket.Id, new { ticket.TicketNo }, ct);

        return OperationResult<WeighTicket>.Ok(ticket);
    }
}

