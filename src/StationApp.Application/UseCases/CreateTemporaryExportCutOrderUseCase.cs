using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CreateTemporaryExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISyncOutboxRepository _syncOutboxRepo;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateTemporaryExportCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        ISyncOutboxRepository syncOutboxRepo,
        ISyncPayloadFactory syncPayloadFactory,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _syncOutboxRepo = syncOutboxRepo;
        _syncPayloadFactory = syncPayloadFactory;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<Guid> ExecuteAsync(CreateTemporaryExportCutOrderRequest request, CancellationToken ct)
    {
        var customerCode = RequireText(request.CustomerCode, "MÃ£ khÃ¡ch hÃ ng");
        var customerName = RequireText(request.CustomerName, "KhÃ¡ch hÃ ng");
        var productCode = RequireText(request.ProductCode, "MÃ£ sáº£n pháº©m");
        var productName = RequireText(request.ProductName, "Sáº£n pháº©m");
        var exportPackageType = RequireExportPackageType(request.ExportPackageType);
        var plannedWeightKg = RequirePositive(request.PlannedWeight, "Sá»‘ lÆ°á»£ng Ä‘áº·t (kg)");
        var isBagged = exportPackageType == ExportPackageTypes.Bagged;
        var tareWeightKg = isBagged
            ? RequireNonNegative(request.TareWeightKg, "Trá»ng lÆ°á»£ng vá» (kg)")
            : 0m;
        var bagWeightKg = isBagged
            ? RequirePositive(request.BagWeightKg, "Trá»ng lÆ°á»£ng bao (kg)")
            : 0m;

        var now = _clock.NowLocal;
        var displayCode = await _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(ct);
        var bagCount = bagWeightKg > 0m ? CalculateBagCount(plannedWeightKg, bagWeightKg) : 0;
        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = null,
            CutOrderSource = CutOrderSource.MANUAL,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            TransactionType = TransactionType.OUTBOUND,
            TransportMethod = TransportMethod.ROAD,
            VehiclePlate = displayCode,
            CustomerCode = customerCode,
            CustomerName = customerName,
            ProductCode = productCode,
            ProductName = productName,
            ProductType = NormalizeOptional(request.ProductType),
            PlannedWeight = plannedWeightKg,
            BagCount = bagCount,
            TareWeightKg = tareWeightKg,
            BagWeightKg = bagWeightKg,
            ExportPackageType = exportPackageType,
            Notes = NormalizeOptional(request.Notes),
            ProcessingStage = ProcessingStage.WEIGHING,
            IsExportScale = true,
            IsTemporaryExport = true,
            IsPortTransfer = request.IsPortTransfer,
            TemporaryExportCreatedReason = request.IsPortTransfer ? "PORT_TRANSFER" : "MANUAL_PRELOAD",
            TemporaryExportDisplayCode = displayCode,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = _userContext.Username
        };

        await _uow.ExecuteInTransactionAsync(
            async innerCt =>
            {
                await _cutOrderRepo.AddAsync(cutOrder, innerCt);
                await EnsureCustomerAsync(customerCode, customerName, now, innerCt);
                await EnsureProductAsync(productCode, productName, request.ProductType, now, innerCt);
            },
            ct);

        return cutOrder.Id;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RequireText(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} lÃ  báº¯t buá»™c.");
        }

        return normalized;
    }

    private static decimal RequirePositive(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value <= 0m)
        {
            throw new InvalidOperationException($"{fieldName} pháº£i lá»›n hÆ¡n 0.");
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal RequireNonNegative(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value < 0m)
        {
            throw new InvalidOperationException($"{fieldName} pháº£i lá»›n hÆ¡n hoáº·c báº±ng 0.");
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

