using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateCutOrderUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CreateCutOrderUseCase(
        ICutOrderRepository regRepo,
        ISyncOutboxRepository outboxRepo,
        IUnitOfWork uow,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncPayloadFactory payloadFactory)
    {
        _regRepo = regRepo;
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<CutOrder>> ExecuteAsync(CreateCutOrderRequest request, CancellationToken ct)
    {
        var reg = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = request.ErpCutOrderId,
            ErpRegistrationCode = request.ErpRegistrationCode?.Trim() ?? request.ErpCutOrderId?.Trim(),
            CutOrderSource = request.CutOrderSource,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            TransactionType = request.TransactionType,
            TransportMethod = request.TransportMethod,
            VehiclePlate = request.VehiclePlate,
            MoocNumber = request.MoocNumber,
            ReceiverName = request.ReceiverName,
            ReceiverIdNo = request.ReceiverIdNo,
            CustomerCode = request.CustomerCode,
            CustomerName = request.CustomerName,
            ProductCode = request.ProductCode,
            ProductName = request.ProductName,
            ProductType = ProductTypes.Normalize(request.ProductType) ?? ProductTypes.InferForTransaction(request.TransactionType),
            OrderCode = request.OrderCode,
            LotNo = request.LotNo,
            RepresentativeName = request.RepresentativeName,
            Market = request.Market,
            ConsumptionPlace = request.ConsumptionPlace,
            LoadingPlace = request.LoadingPlace,
            SealNo = request.SealNo,
            PlannedWeight = request.PlannedWeight,
            BagCount = request.BagCount,
            Notes = request.Notes,
            IsCancelled = false,
            HasOverweightCase = false,
            ProcessingStage = ProcessingStage.IN_YARD,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            IdempotencyKey = Guid.NewGuid(),
            AppVersion = _versionProvider.GetVersion(),
            CreatedAt = _clock.NowLocal,
            CreatedBy = _userContext.Username
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.AddAsync(reg, innerCt);

            var outbox = new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = reg.Id,
                AggregateType = nameof(CutOrder),
                PayloadJson = _payloadFactory.CreatePayload(reg),
                IdempotencyKey = reg.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = _clock.NowLocal
            };
            await _outboxRepo.EnqueueAsync(outbox, innerCt);
        }, ct);

        await _audit.LogAsync("CREATE_VEHICLE_REGISTRATION", nameof(CutOrder), reg.Id, new { reg.VehiclePlate }, ct);

        return OperationResult<CutOrder>.Ok(reg);
    }
}


