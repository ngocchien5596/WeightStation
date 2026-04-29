using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateInboundRegistrationUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;

    public CreateInboundRegistrationUseCase(
        IVehicleRegistrationRepository regRepo,
        IUnitOfWork uow,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit)
    {
        _regRepo = regRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
    }

    public async Task<OperationResult<VehicleRegistration>> ExecuteAsync(CreateInboundRegistrationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VehiclePlate))
            return OperationResult<VehicleRegistration>.Fail("Số PTVC không được để trống.");

        var now = _clock.NowLocal;

        var reg = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.MANUAL,
            RegistrationStatus = RegistrationStatus.REGISTERED,
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
        }, ct);

        await _audit.LogAsync("CREATE_INBOUND_REGISTRATION", nameof(VehicleRegistration), reg.Id,
            new { reg.VehiclePlate, reg.TransactionType }, ct);

        return OperationResult<VehicleRegistration>.Ok(reg);
    }
}
