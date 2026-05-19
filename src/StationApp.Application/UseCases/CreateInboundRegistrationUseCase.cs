using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases.MasterData;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateInboundRegistrationUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly EnsureInboundMasterDataUseCase _ensureInboundMasterDataUseCase;

    public CreateInboundRegistrationUseCase(
        ICutOrderRepository regRepo,
        IUnitOfWork uow,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        EnsureInboundMasterDataUseCase ensureInboundMasterDataUseCase)
    {
        _regRepo = regRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _ensureInboundMasterDataUseCase = ensureInboundMasterDataUseCase;
    }

    public async Task<OperationResult<CutOrder>> ExecuteAsync(CreateInboundRegistrationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VehiclePlate))
            return OperationResult<CutOrder>.Fail("Số PTVC không được để trống.");

        var now = _clock.NowLocal;

        var reg = new CutOrder
        {
            Id = Guid.NewGuid(),
            CutOrderSource = CutOrderSource.MANUAL,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            TransactionType = request.TransactionType,
            TransportMethod = request.TransportMethod,
            ProcessingStage = ProcessingStage.IN_YARD,
            VehiclePlate = request.VehiclePlate,
            MoocNumber = request.MoocNumber,
            ReceiverName = request.ReceiverName,
            CustomerCode = request.CustomerCode,
            CustomerName = request.CustomerName,
            ProductCode = request.ProductCode,
            ProductName = request.ProductName,
            ProductType = ProductTypes.Normalize(request.ProductType) ?? ProductTypes.InferForTransaction(request.TransactionType),
            PlannedWeight = request.PlannedWeight,
            BagCount = request.BagCount,
            Notes = request.Notes,
            IsCancelled = false,
            HasOverweightCase = false,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            IdempotencyKey = Guid.NewGuid(),
            AppVersion = _versionProvider.GetVersion(),
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.AddAsync(reg, innerCt);
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

        await _audit.LogAsync("CREATE_INBOUND_REGISTRATION", nameof(CutOrder), reg.Id,
            new { reg.VehiclePlate, reg.TransactionType }, ct);

        return OperationResult<CutOrder>.Ok(reg);
    }
}

