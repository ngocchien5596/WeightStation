using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class AppendCutOrdersToWeighingSessionUseCase
{
    private static readonly TimeSpan ReuseWeight1Window = TimeSpan.FromHours(24);
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public AppendCutOrdersToWeighingSessionUseCase(
        ICutOrderRepository regRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _regRepo = regRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(AppendCutOrdersToWeighingSessionRequest request, CancellationToken ct)
    {
        if (request.CutOrderIds.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một cắt lệnh để thêm vào lượt cân.");
        }

        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");
        var reuseCutoff = _clock.NowLocal.Subtract(ReuseWeight1Window);

        if (session.Weight1.HasValue
            && (!session.Weight1Time.HasValue || session.Weight1Time.Value < reuseCutoff))
        {
            throw new InvalidOperationException("Lượt cân cũ chỉ được phép dùng lại trong vòng 24 giờ kể từ thời điểm cân lần 1.");
        }

        var existingLines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var sessionRegistrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var activeSessionCutOrderIds = sessionRegistrations.Select(x => x.Id).ToHashSet();
        var orphanLines = existingLines
            .Where(x => !activeSessionCutOrderIds.Contains(x.CutOrderId))
            .ToList();
        var isRecoveringOrphanedAllocationSession =
            session.SessionStatus == WeighingSessionStatus.ALLOCATION_PENDING
            && sessionRegistrations.Count == 0
            && orphanLines.Count > 0;

        if (session.SessionStatus is not WeighingSessionStatus.PENDING_WEIGHT1 and not WeighingSessionStatus.PENDING_WEIGHT2
            && !isRecoveringOrphanedAllocationSession)
        {
            throw new InvalidOperationException("Chỉ được thêm cắt lệnh trước khi lưu cân lần 2.");
        }

        var existingCutOrderIds = activeSessionCutOrderIds;
        if (request.CutOrderIds.Any(existingCutOrderIds.Contains))
        {
            throw new InvalidOperationException("Có cắt lệnh đã nằm trong lượt cân hiện tại.");
        }

        var registrations = await _regRepo.GetByIdsAsync(request.CutOrderIds, ct);
        if (registrations.Count != request.CutOrderIds.Count)
        {
            throw new InvalidOperationException("Có cắt lệnh không còn tồn tại hoặc đã bị thay đổi.");
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

            if (registration.TransactionType != session.TransactionType)
            {
                throw new InvalidOperationException("Không thể thêm cắt lệnh khác loại giao dịch vào lượt cân hiện tại.");
            }

            if (registration.WeighingSessionId.HasValue && registration.WeighingSessionId != session.Id)
            {
                throw new InvalidOperationException($"Cắt lệnh {registration.ErpCutOrderId ?? registration.VehiclePlate} đã thuộc một lượt cân khác.");
            }
        }

        var masterTicket = await _weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, ct);

        var allRegistrations = sessionRegistrations
            .Concat(registrations)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .ToList();

        var now = _clock.NowLocal;
        var activeExistingLines = existingLines
            .Where(x => activeSessionCutOrderIds.Contains(x.CutOrderId))
            .ToList();
        var nextSequence = activeExistingLines.Count == 0 ? 1 : activeExistingLines.Max(x => x.SequenceNo) + 1;
        var newLines = registrations
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .Select((registration, index) => new WeighingSessionLine
            {
                Id = Guid.NewGuid(),
                WeighingSessionId = session.Id,
                CutOrderId = registration.Id,
                SequenceNo = nextSequence + index,
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
            })
            .ToList();

        foreach (var registration in registrations)
        {
            registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
            registration.ProcessingStage = ProcessingStage.WEIGHING;
            registration.WeighingSessionId = session.Id;
            registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        if (masterTicket != null)
        {
            var primaryRegistration = allRegistrations.First();
            masterTicket.CutOrderId = primaryRegistration.Id;
            masterTicket.ErpCutOrderId = primaryRegistration.ErpCutOrderId;
            masterTicket.VehiclePlate = primaryRegistration.VehiclePlate;
            masterTicket.MoocNumber = primaryRegistration.MoocNumber;
            masterTicket.DriverName = primaryRegistration.ReceiverName;
            masterTicket.CustomerCode = primaryRegistration.CustomerCode;
            masterTicket.CustomerName = primaryRegistration.CustomerName;
            masterTicket.ProductCode = primaryRegistration.ProductCode;
            masterTicket.ProductName = primaryRegistration.ProductName;
            masterTicket.PlannedWeight = allRegistrations.Sum(x => x.PlannedWeight ?? 0m);
            masterTicket.BagCount = allRegistrations.Sum(x => x.BagCount ?? 0);
            masterTicket.Notes = allRegistrations.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Notes))?.Notes;
            masterTicket.TransportMethod = primaryRegistration.TransportMethod;
            masterTicket.UpdatedAt = now;
            masterTicket.UpdatedBy = _userContext.Username;

            session.VehiclePlate = primaryRegistration.VehiclePlate;
            session.MoocNumber = primaryRegistration.MoocNumber;
            session.DriverName = primaryRegistration.ReceiverName;
        }

        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var orphanLine in orphanLines)
            {
                orphanLine.IsDeleted = true;
                orphanLine.DeletedAt = now;
                orphanLine.DeletedBy = _userContext.Username;
                orphanLine.LineStatus = WeighingSessionLineStatus.CANCELLED;
                orphanLine.ActualAllocatedWeight = null;
                orphanLine.ActualAllocatedBagCount = null;
                orphanLine.BagCountDisplay = null;
                orphanLine.IsReturnedBrokenTrip = false;
                orphanLine.DeliveryTicketId = null;
                orphanLine.UpdatedAt = now;
                orphanLine.UpdatedBy = _userContext.Username;
                await _sessionRepo.UpdateLineAsync(orphanLine, innerCt);
            }

            foreach (var line in newLines)
            {
                await _sessionRepo.AddLineAsync(line, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            if (masterTicket != null)
            {
                await _weighRepo.UpdateAsync(masterTicket, innerCt);
            }
            await _sessionRepo.UpdateAsync(session, innerCt);
        }, ct);
    }
}
