using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class UpdateTemporaryExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISyncOutboxRepository _syncOutboxRepo;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public UpdateTemporaryExportCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        ISyncOutboxRepository syncOutboxRepo,
        ISyncPayloadFactory syncPayloadFactory,
        IAuditService auditService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _syncOutboxRepo = syncOutboxRepo;
        _syncPayloadFactory = syncPayloadFactory;
        _auditService = auditService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(UpdateTemporaryExportCutOrderRequest request, CancellationToken ct)
    {
        var customerCode = RequireText(request.CustomerCode, "Mã khách hàng");
        var customerName = RequireText(request.CustomerName, "Khách hàng");
        var productCode = RequireText(request.ProductCode, "Mã sản phẩm");
        var productName = RequireText(request.ProductName, "Sản phẩm");
        var exportPackageType = RequireExportPackageType(request.ExportPackageType);
        var productType = NormalizeOptional(request.ProductType);
        var plannedWeightKg = RequirePositive(request.PlannedWeight, "Số lượng đặt (kg)");
        var isBagged = exportPackageType == ExportPackageTypes.Bagged;
        var tareWeightKg = isBagged
            ? RequireNonNegative(request.TareWeightKg, "Trọng lượng vỏ (kg)")
            : 0m;
        var bagWeightKg = isBagged
            ? RequirePositive(request.BagWeightKg, "Trọng lượng bao (kg)")
            : 0m;

        var notes = NormalizeOptional(request.Notes);

        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh tạm.");
        ValidateEditableTemporaryCutOrder(cutOrder);

        var oldState = CreateAuditSnapshot(cutOrder);
        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        var sessions = new List<WeighingSession>();
        var lines = new List<WeighingSessionLine>();
        var weighTickets = new List<WeighTicket>();
        var deliveryTickets = new List<DeliveryTicket>();

        foreach (var trip in trips)
        {
            var session = await _sessionRepo.GetByIdAsync(trip.SessionId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy chuyến xe thuộc cắt lệnh tạm.");
            var sessionLines = (await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == cutOrder.Id)
                .ToList();

            sessions.Add(session);
            lines.AddRange(sessionLines);
            weighTickets.AddRange((await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == cutOrder.Id));
            deliveryTickets.AddRange((await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == cutOrder.Id));
        }

        var now = _clock.NowLocal;
        var username = _userContext.Username;
        var bagCount = isBagged ? CalculateBagCount(plannedWeightKg, bagWeightKg) : 0;

        cutOrder.CustomerCode = customerCode;
        cutOrder.CustomerName = customerName;
        cutOrder.ProductCode = productCode;
        cutOrder.ProductName = productName;
        cutOrder.ProductType = productType;
        cutOrder.PlannedWeight = plannedWeightKg;
        cutOrder.BagCount = bagCount;
        cutOrder.TareWeightKg = tareWeightKg;
        cutOrder.BagWeightKg = bagWeightKg;
        cutOrder.ExportPackageType = exportPackageType;
        cutOrder.Notes = notes;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = username;

        foreach (var line in lines)
        {
            line.CustomerCode = customerCode;
            line.CustomerName = customerName;
            line.DistributorCode = customerCode;
            line.DistributorName = customerName;
            line.ProductCode = productCode;
            line.ProductName = productName;
            line.PlannedWeight = plannedWeightKg;
            line.PlannedBagCount = bagCount;
            line.SyncStatus = SyncStatus.SYNC_QUEUED;
            line.LastSyncAttemptAt = null;
            line.LastSyncError = null;
            line.UpdatedAt = now;
            line.UpdatedBy = username;
        }

        foreach (var session in sessions)
        {
            session.SyncStatus = SyncStatus.SYNC_QUEUED;
            session.LastSyncAttemptAt = null;
            session.LastSyncError = null;
            session.UpdatedAt = now;
            session.UpdatedBy = username;
        }

        foreach (var ticket in weighTickets)
        {
            ticket.CustomerCode = customerCode;
            ticket.CustomerName = customerName;
            ticket.ProductCode = productCode;
            ticket.ProductName = productName;
            ticket.PlannedWeight = plannedWeightKg;
            ticket.BagCount = bagCount;
            ticket.Notes = notes;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        foreach (var ticket in deliveryTickets)
        {
            ticket.CustomerCode = customerCode;
            ticket.ProductCode = productCode;
            ticket.Notes = notes;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        var newState = CreateAuditSnapshot(cutOrder);
        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var session in sessions)
            {
                await _sessionRepo.UpdateAsync(session, innerCt);
            }

            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var ticket in weighTickets)
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(ticket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
            await EnsureCustomerAsync(customerCode, customerName, now, innerCt);
            await EnsureProductAsync(productCode, productName, productType, now, innerCt);
        }, ct);

        await _auditService.LogAsync(
            "UPDATE_TEMPORARY_EXPORT_CUT_ORDER",
            "CutOrder",
            cutOrder.Id,
            new
            {
                DisplayCode = cutOrder.TemporaryExportDisplayCode ?? cutOrder.ErpCutOrderId,
                TripCount = trips.Count,
                UpdatedSessionCount = sessions.Count,
                UpdatedLineCount = lines.Count,
                UpdatedWeighTicketCount = weighTickets.Count,
                UpdatedDeliveryTicketCount = deliveryTickets.Count,
                Old = oldState,
                New = newState
            },
            ct);
    }

    private static void ValidateEditableTemporaryCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsTemporaryExport || !cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Chỉ được sửa cắt lệnh xuất khẩu tạm.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã chốt tổng, không được sửa.");
        }

        if (cutOrder.MappedRealCutOrderId.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã map sang cắt lệnh thật, không được sửa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND
            || cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION
            || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh tạm không ở trạng thái cân xuất khẩu đang hoạt động.");
        }
    }

    private static object CreateAuditSnapshot(CutOrder cutOrder)
        => new
        {
            cutOrder.CustomerCode,
            cutOrder.CustomerName,
            cutOrder.ProductCode,
            cutOrder.ProductName,
            cutOrder.ProductType,
            cutOrder.PlannedWeight,
            cutOrder.BagCount,
            cutOrder.TareWeightKg,
            cutOrder.BagWeightKg,
            cutOrder.ExportPackageType,
            cutOrder.Notes
        };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RequireText(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} là bắt buộc.");
        }

        return normalized;
    }

    private static decimal RequirePositive(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value <= 0m)
        {
            throw new InvalidOperationException($"{fieldName} phải lớn hơn 0.");
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal RequireNonNegative(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value < 0m)
        {
            throw new InvalidOperationException($"{fieldName} phải lớn hơn hoặc bằng 0.");
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
    }

    private static string RequireExportPackageType(string? value)
    {
        var normalized = ExportPackageTypes.Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Lo\u1ea1i l\u00e0 b\u1eaft bu\u1ed9c.");
        }

        return normalized;
    }

    private static int CalculateBagCount(decimal plannedWeightKg, decimal bagWeightKg)
    {
        var exact = plannedWeightKg / bagWeightKg;
        return (int)decimal.Round(exact, 0, MidpointRounding.AwayFromZero);
    }

    private async Task EnsureCustomerAsync(string? customerCode, string customerName, DateTime now, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(customerCode);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var existing = await _customerRepo.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            existing = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = normalizedCode,
                CustomerName = customerName,
                CustomerBusinessRole = CustomerBusinessRoles.Distributor,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };
            await _customerRepo.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.Equals(existing.CustomerName, customerName, StringComparison.Ordinal))
        {
            existing.CustomerName = customerName;
            changed = true;
        }

        var mergedRole = CustomerBusinessRoles.MergeForTransaction(existing.CustomerBusinessRole, TransactionType.OUTBOUND);
        if (!string.Equals(existing.CustomerBusinessRole, mergedRole, StringComparison.Ordinal))
        {
            existing.CustomerBusinessRole = mergedRole;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = now;
        existing.UpdatedBy = _userContext.Username;
        await _customerRepo.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnsureProductAsync(string? productCode, string productName, string? productType, DateTime now, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(productCode);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var normalizedType = ProductTypes.Normalize(productType) ?? ProductTypes.InferForTransaction(TransactionType.OUTBOUND);
        var existing = await _productRepo.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            existing = new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = normalizedCode,
                ProductName = productName,
                ProductType = normalizedType,
                TransactionScope = ProductTransactionScopes.Outbound,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };
            await _productRepo.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.Equals(existing.ProductName, productName, StringComparison.Ordinal))
        {
            existing.ProductName = productName;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedType)
            && !string.Equals(existing.ProductType, normalizedType, StringComparison.Ordinal))
        {
            existing.ProductType = normalizedType;
            changed = true;
        }

        var mergedScope = ProductTransactionScopes.MergeForTransaction(existing.TransactionScope, TransactionType.OUTBOUND);
        if (!string.Equals(existing.TransactionScope, mergedScope, StringComparison.Ordinal))
        {
            existing.TransactionScope = mergedScope;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = now;
        existing.UpdatedBy = _userContext.Username;
        await _productRepo.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnqueueMasterSyncAsync(
        Guid aggregateId,
        string aggregateType,
        string payloadJson,
        DateTime now,
        CancellationToken ct)
    {
        await _syncOutboxRepo.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            PayloadJson = payloadJson,
            IdempotencyKey = aggregateId,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now
        }, ct);
    }
}
