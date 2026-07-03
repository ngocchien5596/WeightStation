using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateWeighingSessionUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IWeighingSessionNumberGenerator _sessionNoGen;
    private readonly ITicketNumberGenerator _ticketNoGen;

    public CreateWeighingSessionUseCase(
        ICutOrderRepository regRepo,
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        IWeighTicketRepository weighRepo,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IWeighingSessionNumberGenerator sessionNoGen,
        ITicketNumberGenerator ticketNoGen)
    {
        _regRepo = regRepo;
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _weighRepo = weighRepo;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _sessionNoGen = sessionNoGen;
        _ticketNoGen = ticketNoGen;
    }

    public async Task<CreateWeighingSessionResult> ExecuteAsync(CreateWeighingSessionRequest request, CancellationToken ct)
    {
        if (request.ApplyCarryForwardWeight1)
        {
            throw new InvalidOperationException("Chỉ được dùng lại cân lần 1 khi gắn vào lượt cân cũ phù hợp.");
        }

        if (request.CutOrderIds.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một cắt lệnh để tạo lượt cân.");
        }

        var registrations = await _regRepo.GetByIdsAsync(request.CutOrderIds, ct);
        if (registrations.Count != request.CutOrderIds.Count)
        {
            throw new InvalidOperationException("Có cắt lệnh không còn tồn tại hoặc đã bị thay đổi.");
        }

        var first = registrations[0];
        var primaryRegistration = request.PrimaryCutOrderId.HasValue
            ? registrations.FirstOrDefault(x => x.Id == request.PrimaryCutOrderId.Value)
            : null;
        primaryRegistration ??= first;
        if (primaryRegistration.TransactionType == TransactionType.INBOUND && registrations.Count > 1)
        {
            throw new InvalidOperationException("Giai đoạn hiện tại chưa hỗ trợ gộp nhiều phiếu nhập hàng vào một lượt cân.");
        }

        foreach (var registration in registrations)
        {
            if (registration.IsCancelled)
            {
                throw new InvalidOperationException($"Cắt lệnh {registration.ErpCutOrderId ?? registration.VehiclePlate} đã bị hủy.");
            }

            if (registration.ProcessingStage != ProcessingStage.IN_YARD || registration.CutOrderStatus != CutOrderStatus.REGISTERED)
            {
                throw new InvalidOperationException($"Cắt lệnh {registration.ErpCutOrderId ?? registration.VehiclePlate} không còn ở hàng xe vào.");
            }

            if (registration.TransactionType != primaryRegistration.TransactionType)
            {
                throw new InvalidOperationException("Không thể gộp cắt lệnh nhập và xuất trong cùng một lượt cân.");
            }
        }

        var orderedRegistrations = registrations
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .ToList();

        Guid? sessionId = null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.NowLocal;
            var sessionNo = await _sessionNoGen.GenerateAsync(primaryRegistration.TransactionType, innerCt);
            var session = new WeighingSession
            {
                Id = Guid.NewGuid(),
                SessionNo = sessionNo,
                TransactionType = primaryRegistration.TransactionType,
                VehiclePlate = primaryRegistration.VehiclePlate,
                MoocNumber = primaryRegistration.MoocNumber,
                DriverName = primaryRegistration.ReceiverName,
                SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
                OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
                OverweightAmount = 0m,
                IsCancelled = false,
                HasPrintedMasterWeighTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            var lines = orderedRegistrations.Select((registration, index) => new WeighingSessionLine
            {
                Id = Guid.NewGuid(),
                WeighingSessionId = session.Id,
                CutOrderId = registration.Id,
                SequenceNo = index + 1,
                CustomerCode = registration.CustomerCode,
                CustomerName = registration.CustomerName,
                DistributorName = registration.CustomerName,
                ProductCode = registration.ProductCode,
                ProductName = registration.ProductName,
                PlannedWeight = registration.PlannedWeight,
                PlannedBagCount = registration.BagCount,
                LineStatus = WeighingSessionLineStatus.PENDING,
                HasPrintedDeliveryTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            }).ToList();

            foreach (var registration in orderedRegistrations)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.WeighingSessionId = session.Id;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
                registration.UpdatedAt = now;
                registration.UpdatedBy = _userContext.Username;
            }

            await _sessionRepo.AddAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.AddLineAsync(line, innerCt);
            }

            foreach (var registration in orderedRegistrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            sessionId = session.Id;
        }, ct);

        return new CreateWeighingSessionResult(sessionId!.Value);
    }
}
