using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class MarkRegistrationsNoLoadUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IWeighingSessionNumberGenerator _sessionNoGen;

    public MarkRegistrationsNoLoadUseCase(
        ICutOrderRepository regRepo,
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IWeighingSessionNumberGenerator sessionNoGen)
    {
        _regRepo = regRepo;
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _sessionNoGen = sessionNoGen;
    }

    public async Task<Guid> ExecuteAsync(MarkRegistrationsNoLoadRequest request, CancellationToken ct)
    {
        if (request.CutOrderIds.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một cắt lệnh để chuyển xe ra.");
        }

        var registrations = await _regRepo.GetByIdsAsync(request.CutOrderIds, ct);
        if (registrations.Count != request.CutOrderIds.Count)
        {
            throw new InvalidOperationException("Có cắt lệnh không còn tồn tại hoặc đã bị thay đổi.");
        }

        var first = registrations[0];
        if (first.TransactionType == TransactionType.INBOUND && registrations.Count > 1)
        {
            throw new InvalidOperationException("Giai đoạn hiện tại chưa hỗ trợ gộp nhiều phiếu nhập hàng.");
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

            if (registration.TransactionType != first.TransactionType)
            {
                throw new InvalidOperationException("Không thể gộp cắt lệnh nhập và xuất.");
            }
        }

        Guid? sessionId = null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.NowLocal;
            var sessionNo = await _sessionNoGen.GenerateAsync(first.TransactionType, innerCt);
            var session = new WeighingSession
            {
                Id = Guid.NewGuid(),
                SessionNo = sessionNo,
                TransactionType = first.TransactionType,
                VehiclePlate = first.VehiclePlate,
                MoocNumber = first.MoocNumber,
                DriverName = first.ReceiverName,
                SessionStatus = WeighingSessionStatus.COMPLETED,
                NetWeight = 0m,
                OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
                OverweightAmount = 0m,
                IsCancelled = false,
                IsNoLoad = true,
                HasPrintedMasterWeighTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username,
                UpdatedAt = now,
                UpdatedBy = _userContext.Username
            };

            var lines = registrations.Select((registration, index) => new WeighingSessionLine
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
                ActualAllocatedWeight = 0m,
                ActualAllocatedBagCount = 0,
                BagCountDisplay = 0,
                LineStatus = WeighingSessionLineStatus.ALLOCATED,
                HasPrintedDeliveryTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username,
                UpdatedAt = now,
                UpdatedBy = _userContext.Username
            }).ToList();

            foreach (var registration in registrations)
            {
                registration.CutOrderStatus = CutOrderStatus.COMPLETED;
                registration.ProcessingStage = ProcessingStage.OUT_YARD;
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

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            sessionId = session.Id;
        }, ct);

        return sessionId!.Value;
    }
}
