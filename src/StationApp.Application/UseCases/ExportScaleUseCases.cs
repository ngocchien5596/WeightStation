using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class TransitionToExportScaleUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public TransitionToExportScaleUseCase(
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(TransitionToExportScaleRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Ch\u1ec9 h\u1ed7 tr\u1ee3 c\u00e2n xu\u1ea5t kh\u1ea9u cho c\u1eaft l\u1ec7nh xu\u1ea5t h\u00e0ng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 ch\u1ed1t, kh\u00f4ng th\u1ec3 chuy\u1ec3n sang c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (!cutOrder.IsExportScale
            && (cutOrder.CutOrderStatus != CutOrderStatus.REGISTERED || cutOrder.ProcessingStage != ProcessingStage.IN_YARD))
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng c\u00f2n \u1edf tr\u1ea1ng th\u00e1i xe v\u00e0o \u0111\u1ec3 chuy\u1ec3n sang c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (!cutOrder.IsExportScale && cutOrder.WeighingSessionId.HasValue)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 thu\u1ed9c m\u1ed9t l\u01b0\u1ee3t c\u00e2n kh\u00e1c.");
        }

        var now = _clock.NowLocal;
        cutOrder.IsExportScale = true;
        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.ExportStartedAt ??= now;
        cutOrder.ExportStartedBy ??= _userContext.Username;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}

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
        var customerCode = RequireText(request.CustomerCode, "Mã khách hàng");
        var customerName = RequireText(request.CustomerName, "Kh\u00e1ch h\u00e0ng");
        var productCode = RequireText(request.ProductCode, "Mã sản phẩm");
        var productName = RequireText(request.ProductName, "S\u1ea3n ph\u1ea9m");
        var plannedWeightKg = RequirePositive(request.PlannedWeight, "S\u1ed1 l\u01b0\u1ee3ng \u0111\u1eb7t (kg)");
        var tareWeightKg = RequireNonNegative(request.TareWeightKg, "Tr\u1ecdng l\u01b0\u1ee3ng v\u1ecf (kg)");
        var bagWeightKg = RequireNonNegative(request.BagWeightKg, "Trọng lượng bao (kg)");

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
            Notes = NormalizeOptional(request.Notes),
            ProcessingStage = ProcessingStage.WEIGHING,
            IsExportScale = true,
            IsTemporaryExport = true,
            TemporaryExportCreatedReason = "MANUAL_PRELOAD",
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
            throw new InvalidOperationException($"{fieldName} l\u00e0 b\u1eaft bu\u1ed9c.");
        }

        return normalized;
    }

    private static decimal RequirePositive(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value <= 0m)
        {
            throw new InvalidOperationException($"{fieldName} ph\u1ea3i l\u1edbn h\u01a1n 0.");
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal RequireNonNegative(decimal? value, string fieldName)
    {
        if (!value.HasValue || value.Value < 0m)
        {
            throw new InvalidOperationException($"{fieldName} ph\u1ea3i l\u1edbn h\u01a1n ho\u1eb7c b\u1eb1ng 0.");
        }

        return decimal.Round(value.Value, 3, MidpointRounding.AwayFromZero);
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

public sealed class MapTemporaryExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public MapTemporaryExportCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(MapTemporaryExportCutOrderRequest request, CancellationToken ct)
    {
        var temporaryCutOrder = await _cutOrderRepo.GetByIdAsync(request.TemporaryCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh tạm.");
        var realCutOrder = await _cutOrderRepo.GetByIdAsync(request.RealCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh thật.");

        ValidateRealCutOrder(realCutOrder);

        if (temporaryCutOrder.Id == realCutOrder.Id)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(temporaryCutOrder.Id, ct);
        NormalizeRecoverableTemporaryCutOrderState(temporaryCutOrder, trips.Count);
        ValidateTemporaryCutOrder(temporaryCutOrder);

        var sessions = new List<WeighingSession>();
        var lines = new List<WeighingSessionLine>();
        var weighTickets = new List<WeighTicket>();
        var deliveryTickets = new List<DeliveryTicket>();

        foreach (var trip in trips)
        {
            var session = await _sessionRepo.GetByIdAsync(trip.SessionId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy chuyến xe thuộc cắt lệnh tạm.");
            var sessionLines = (await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == temporaryCutOrder.Id)
                .ToList();

            if (sessionLines.Count != 1)
            {
                throw new InvalidOperationException("Chỉ hỗ trợ map chuyến xe xuất khẩu có đúng 1 dòng cắt lệnh.");
            }

            sessions.Add(session);
            lines.Add(sessionLines[0]);
            weighTickets.AddRange((await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct)).Where(x => !x.IsDeleted));
            deliveryTickets.AddRange((await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct)).Where(x => !x.IsDeleted));
        }

        var now = _clock.NowLocal;
        var username = _userContext.Username;
        var targetExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(realCutOrder.Id, ct);
        var targetExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(realCutOrder.Id, ct);

        realCutOrder.IsExportScale = true;
        realCutOrder.IsTemporaryExport = false;
        realCutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        realCutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        realCutOrder.WeighingSessionId = null;
        realCutOrder.ExportStartedAt ??= now;
        realCutOrder.ExportStartedBy ??= username;
        realCutOrder.TareWeightKg ??= temporaryCutOrder.TareWeightKg;
        realCutOrder.BagWeightKg ??= temporaryCutOrder.BagWeightKg;
        realCutOrder.BagCount ??= temporaryCutOrder.BagCount;
        realCutOrder.MappedTemporaryCutOrderId = temporaryCutOrder.Id;
        realCutOrder.MappedAt = now;
        realCutOrder.MappedBy = username;
        realCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        realCutOrder.UpdatedAt = now;
        realCutOrder.UpdatedBy = username;

        temporaryCutOrder.MappedRealCutOrderId = realCutOrder.Id;
        temporaryCutOrder.MappedAt = now;
        temporaryCutOrder.MappedBy = username;
        temporaryCutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        temporaryCutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        temporaryCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        temporaryCutOrder.UpdatedAt = now;
        temporaryCutOrder.UpdatedBy = username;

        var targetPlannedWeight = ResolveTargetPlannedWeight(realCutOrder);
        foreach (var line in lines)
        {
            line.CutOrderId = realCutOrder.Id;
            line.CustomerCode = realCutOrder.CustomerCode;
            line.CustomerName = realCutOrder.CustomerName;
            line.DistributorCode = realCutOrder.CustomerCode;
            line.DistributorName = realCutOrder.CustomerName;
            line.ProductCode = realCutOrder.ProductCode;
            line.ProductName = realCutOrder.ProductName;
            line.PlannedWeight = targetPlannedWeight;
            line.PlannedBagCount = realCutOrder.BagCount;
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
            ticket.CutOrderId = realCutOrder.Id;
            ticket.ErpCutOrderId = realCutOrder.ErpCutOrderId;
            ticket.CustomerCode = realCutOrder.CustomerCode;
            ticket.CustomerName = realCutOrder.CustomerName;
            ticket.ProductCode = realCutOrder.ProductCode;
            ticket.ProductName = realCutOrder.ProductName;
            ticket.PlannedWeight = targetPlannedWeight;
            ticket.BagCount = realCutOrder.BagCount;
            ticket.Notes = realCutOrder.Notes;
            ticket.TransportMethod = realCutOrder.TransportMethod;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        foreach (var ticket in deliveryTickets)
        {
            ticket.CutOrderId = realCutOrder.Id;
            ticket.ErpCutOrderId = realCutOrder.ErpCutOrderId ?? string.Empty;
            ticket.CustomerCode = realCutOrder.CustomerCode;
            ticket.ProductCode = realCutOrder.ProductCode;
            ticket.Notes = realCutOrder.Notes;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        realCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(targetExistingWeighTickets.Concat(weighTickets))?.Id;
        realCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(targetExistingDeliveryTickets.Concat(deliveryTickets))?.Id;
        temporaryCutOrder.CurrentPrimaryWeighTicketId = null;
        temporaryCutOrder.CurrentPrimaryDeliveryTicketId = null;

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

            await _cutOrderRepo.UpdateAsync(temporaryCutOrder, innerCt);
            await _cutOrderRepo.UpdateAsync(realCutOrder, innerCt);
        }, ct);
    }

    private static void ValidateTemporaryCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsTemporaryExport || !cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn không phải cắt lệnh xuất khẩu tạm.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND
            || cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION
            || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh tạm không ở trạng thái cân xuất khẩu đang hoạt động.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã chốt tổng, không thể map.");
        }
    }

    private static void NormalizeRecoverableTemporaryCutOrderState(CutOrder cutOrder, int tripCount)
    {
        if (!cutOrder.IsTemporaryExport
            || !cutOrder.IsExportScale
            || cutOrder.IsDeleted
            || cutOrder.IsCancelled
            || cutOrder.ExportFinalizedAt.HasValue
            || cutOrder.TransactionType != TransactionType.OUTBOUND
            || tripCount <= 0)
        {
            return;
        }

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
    }

    private static void ValidateRealCutOrder(CutOrder cutOrder)
    {
        if (cutOrder.IsTemporaryExport)
        {
            throw new InvalidOperationException("Cắt lệnh đích phải là cắt lệnh thật từ ERP.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ map sang cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã chốt tổng, không thể map.");
        }

        if (cutOrder.IsExportScale)
        {
            if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
            {
                throw new InvalidOperationException("Cắt lệnh thật không ở trạng thái cân xuất khẩu.");
            }

            return;
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.REGISTERED || cutOrder.ProcessingStage != ProcessingStage.IN_YARD)
        {
            throw new InvalidOperationException("Cắt lệnh thật không còn ở trạng thái xe vào để map sang cân xuất khẩu.");
        }

        if (cutOrder.WeighingSessionId.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã thuộc một lượt cân khác.");
        }
    }

    private static decimal? ResolveTargetPlannedWeight(CutOrder cutOrder)
        => cutOrder.PlannedWeight;

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }
}

public sealed class CreateExportVehicleSessionUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighingSessionNumberGenerator _sessionNoGen;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CreateExportVehicleSessionUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IVehicleRepository vehicleRepo,
        IWeighingSessionNumberGenerator sessionNoGen,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _vehicleRepo = vehicleRepo;
        _sessionNoGen = sessionNoGen;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task<CreateExportVehicleSessionResult> ExecuteAsync(CreateExportVehicleSessionRequest request, CancellationToken ct)
    {
        var vehiclePlate = request.VehiclePlate?.Trim();
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            throw new InvalidOperationException("Vui l\u00f2ng nh\u1eadp bi\u1ec3n s\u1ed1 xe cho chuy\u1ebfn xu\u1ea5t kh\u1ea9u.");
        }

        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        NormalizeRecoverableExportCutOrderState(cutOrder);
        ValidateOpenExportCutOrder(cutOrder);

        CreateExportVehicleSessionResult? result = null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.NowLocal;
            var sessionNo = await _sessionNoGen.GenerateAsync(TransactionType.OUTBOUND, innerCt);
            var plannedWeightForTrip = await ResolveRemainingPlannedWeightAsync(cutOrder, innerCt);

            var session = new WeighingSession
            {
                Id = Guid.NewGuid(),
                SessionNo = sessionNo,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = vehiclePlate,
                MoocNumber = NormalizeOptional(request.MoocNumber),
                DriverName = NormalizeOptional(request.DriverName),
                SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
                OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
                OverweightAmount = 0m,
                IsCancelled = false,
                HasPrintedMasterWeighTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            var line = new WeighingSessionLine
            {
                Id = Guid.NewGuid(),
                WeighingSessionId = session.Id,
                CutOrderId = cutOrder.Id,
                SequenceNo = 1,
                CustomerCode = cutOrder.CustomerCode,
                CustomerName = cutOrder.CustomerName,
                DistributorName = cutOrder.CustomerName,
                ProductCode = cutOrder.ProductCode,
                ProductName = cutOrder.ProductName,
                PlannedWeight = plannedWeightForTrip,
                PlannedBagCount = cutOrder.BagCount,
                LineStatus = WeighingSessionLineStatus.PENDING,
                HasPrintedDeliveryTicket = false,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            cutOrder.WeighingSessionId = null;
            cutOrder.UpdatedAt = now;
            cutOrder.UpdatedBy = _userContext.Username;

            await UpsertVehicleMasterAsync(request, vehiclePlate, now, innerCt);
            await _sessionRepo.AddAsync(session, innerCt);
            await _sessionRepo.AddLineAsync(line, innerCt);
            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);

            result = new CreateExportVehicleSessionResult(session.Id, session.SessionNo);
        }, ct);

        return result!;
    }

    private async Task UpsertVehicleMasterAsync(
        CreateExportVehicleSessionRequest request,
        string vehiclePlate,
        DateTime now,
        CancellationToken ct)
    {
        var moocNumber = NormalizeOptional(request.MoocNumber);
        Vehicle? vehicle = null;

        if (!string.IsNullOrWhiteSpace(moocNumber))
        {
            vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(vehiclePlate, moocNumber, ct);
        }

        var samePlateVehicles = await _vehicleRepo.GetByPlateAsync(vehiclePlate, ct);
        vehicle ??= string.IsNullOrWhiteSpace(moocNumber)
            ? samePlateVehicles.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.MoocNumber)) ?? samePlateVehicles.FirstOrDefault()
            : samePlateVehicles.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.MoocNumber));

        if (vehicle == null)
        {
            vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = vehiclePlate,
                MoocNumber = moocNumber ?? string.Empty,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _userContext.Username
            };

            ApplyVehicleMasterPayload(vehicle, request, moocNumber);
            await _vehicleRepo.AddAsync(vehicle, ct);
            return;
        }

        vehicle.VehiclePlate = vehiclePlate;
        vehicle.MoocNumber = moocNumber ?? string.Empty;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = _userContext.Username;
        ApplyVehicleMasterPayload(vehicle, request, moocNumber);
        await _vehicleRepo.UpdateAsync(vehicle, ct);
    }

    private static void ApplyVehicleMasterPayload(
        Vehicle vehicle,
        CreateExportVehicleSessionRequest request,
        string? moocNumber)
    {
        vehicle.DriverName = NormalizeOptional(request.DriverName);
        if (request.TtcpWeight is > 0m)
        {
            vehicle.TtcpWeight = request.TtcpWeight;
        }

        vehicle.VehicleRegistrationNo = NormalizeOptional(request.VehicleRegistrationNo);
        vehicle.VehicleRegistrationExpiryDate = request.VehicleRegistrationExpiryDate;
        vehicle.MoocNumber = moocNumber ?? string.Empty;
        vehicle.MoocRegistrationNo = NormalizeOptional(request.MoocRegistrationNo);
        vehicle.MoocRegistrationExpiryDate = request.MoocRegistrationExpiryDate;
    }

    private async Task<decimal?> ResolveRemainingPlannedWeightAsync(CutOrder cutOrder, CancellationToken ct)
    {
        var activeSummary = await _cutOrderRepo.GetActiveExportScaleCutOrdersAsync(
            new ExportScaleCutOrderFilter(cutOrder.ErpCutOrderId, null, null, null, null),
            ct);
        var currentSummary = activeSummary.FirstOrDefault(x => x.CutOrderId == cutOrder.Id);
        return currentSummary != null && currentSummary.RemainingWeight > 0m
            ? currentSummary.RemainingWeight
            : cutOrder.PlannedWeight;
    }

    private static void ValidateOpenExportCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh ch\u01b0a \u0111\u01b0\u1ee3c chuy\u1ec3n sang lu\u1ed3ng c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Ch\u1ec9 h\u1ed7 tr\u1ee3 c\u00e2n xu\u1ea5t kh\u1ea9u cho c\u1eaft l\u1ec7nh xu\u1ea5t h\u00e0ng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 ch\u1ed1t, kh\u00f4ng th\u1ec3 t\u1ea1o th\u00eam chuy\u1ebfn xe.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng \u1edf tr\u1ea1ng th\u00e1i c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }
    }

    private static void NormalizeRecoverableExportCutOrderState(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale
            || cutOrder.TransactionType != TransactionType.OUTBOUND
            || cutOrder.IsDeleted
            || cutOrder.IsCancelled
            || cutOrder.ExportFinalizedAt.HasValue
            || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var recoverableStatus = cutOrder.CutOrderStatus is CutOrderStatus.REGISTERED or CutOrderStatus.IN_SESSION;
        var recoverableStage = cutOrder.ProcessingStage is ProcessingStage.IN_YARD or ProcessingStage.WEIGHING;
        if (!recoverableStatus || !recoverableStage)
        {
            return;
        }

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class TransferExportVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepository;

    public TransferExportVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepository)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepository = auditLogRepository;
    }

    public async Task ExecuteAsync(TransferExportVehicleTripRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe cần chuyển.");

        if (session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến xe đã bị hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var activeLines = lines.Where(x => !x.IsDeleted).ToList();
        if (activeLines.Count != 1)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến xe xuất khẩu có đúng 1 dòng cắt lệnh.");
        }

        var line = activeLines[0];
        var sourceCutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh nguồn của chuyến xe.");
        var targetCutOrder = await _cutOrderRepo.GetByIdAsync(request.TargetCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh đích.");

        ValidateTransferSourceCutOrder(sourceCutOrder);
        ValidateTransferTargetCutOrder(targetCutOrder);

        if (sourceCutOrder.Id == targetCutOrder.Id)
        {
            return;
        }

        var now = _clock.NowLocal;
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var sourceExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var sourceExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var targetExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetPlannedWeight = await ResolveRemainingPlannedWeightAsync(targetCutOrder, ct);

        line.CutOrderId = targetCutOrder.Id;
        line.CustomerCode = targetCutOrder.CustomerCode;
        line.CustomerName = targetCutOrder.CustomerName;
        line.DistributorCode = targetCutOrder.CustomerCode;
        line.DistributorName = targetCutOrder.CustomerName;
        line.ProductCode = targetCutOrder.ProductCode;
        line.ProductName = targetCutOrder.ProductName;
        line.PlannedWeight = targetPlannedWeight;
        line.PlannedBagCount = targetCutOrder.BagCount;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        foreach (var weighTicket in weighTickets.Where(x => !x.IsDeleted))
        {
            weighTicket.CutOrderId = targetCutOrder.Id;
            weighTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId;
            weighTicket.CustomerCode = targetCutOrder.CustomerCode;
            weighTicket.CustomerName = targetCutOrder.CustomerName;
            weighTicket.ProductCode = targetCutOrder.ProductCode;
            weighTicket.ProductName = targetCutOrder.ProductName;
            weighTicket.PlannedWeight = targetPlannedWeight;
            weighTicket.BagCount = targetCutOrder.BagCount;
            weighTicket.Notes = targetCutOrder.Notes;
            weighTicket.TransportMethod = targetCutOrder.TransportMethod;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets.Where(x => !x.IsDeleted))
        {
            deliveryTicket.CutOrderId = targetCutOrder.Id;
            deliveryTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId ?? string.Empty;
            deliveryTicket.CustomerCode = targetCutOrder.CustomerCode;
            deliveryTicket.ProductCode = targetCutOrder.ProductCode;
            deliveryTicket.Notes = targetCutOrder.Notes;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        var sourceCurrentPrimaryWeighTicketId = sourceCutOrder.CurrentPrimaryWeighTicketId;
        var sourceCurrentPrimaryDeliveryTicketId = sourceCutOrder.CurrentPrimaryDeliveryTicketId;

        sourceCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        sourceCutOrder.UpdatedAt = now;
        sourceCutOrder.UpdatedBy = _userContext.Username;

        targetCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        targetCutOrder.UpdatedAt = now;
        targetCutOrder.UpdatedBy = _userContext.Username;
        targetCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(targetExistingWeighTickets.Concat(weighTickets))?.Id;
        targetCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(targetExistingDeliveryTickets.Concat(deliveryTickets))?.Id;

        if (sourceCurrentPrimaryWeighTicketId.HasValue && weighTickets.Any(x => x.Id == sourceCurrentPrimaryWeighTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(
                sourceExistingWeighTickets.Where(x => weighTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        if (sourceCurrentPrimaryDeliveryTicketId.HasValue && deliveryTickets.Any(x => x.Id == sourceCurrentPrimaryDeliveryTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(
                sourceExistingDeliveryTickets.Where(x => deliveryTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateLineAsync(line, innerCt);

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(sourceCutOrder, innerCt);
            await _cutOrderRepo.UpdateAsync(targetCutOrder, innerCt);
        }, ct);

        // Log the transfer
        var sourceErpId = sourceCutOrder.ErpCutOrderId;
        var targetErpId = targetCutOrder.ErpCutOrderId;

        // Với temporary cut order, dùng TemporaryExportDisplayCode làm display code thay thế
        var sourceDisplayCode = sourceCutOrder.IsTemporaryExport
            ? sourceCutOrder.TemporaryExportDisplayCode ?? sourceErpId
            : sourceErpId;
        var targetDisplayCode = targetCutOrder.IsTemporaryExport
            ? targetCutOrder.TemporaryExportDisplayCode ?? targetErpId
            : targetErpId;

        var auditDetail = new
        {
            SessionNo = session.SessionNo,
            SourceCutOrderId = sourceCutOrder.Id,
            SourceErpCutOrderId = sourceErpId,
            SourceDisplayCode = sourceDisplayCode,
            TargetCutOrderId = targetCutOrder.Id,
            TargetErpCutOrderId = targetErpId,
            TargetDisplayCode = targetDisplayCode,
            VehiclePlate = session.VehiclePlate,
            Weight1 = session.Weight1,
            Weight2 = session.Weight2,
            NetWeight = session.NetWeight,
            Reason = $"Chuyển chuyến từ cắt lệnh {sourceDisplayCode ?? sourceCutOrder.Id.ToString()} sang {targetDisplayCode ?? targetCutOrder.Id.ToString()}"
        };

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TRANSFER_EXPORT_TRIP",
            EntityType = "WeighingSession",
            EntityId = session.Id,
            DetailJson = System.Text.Json.JsonSerializer.Serialize(auditDetail, new System.Text.Json.JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = _clock.NowLocal,
            StationCode = _userContext.StationCode
        };

        await _auditLogRepository.AddAsync(auditLog, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<decimal?> ResolveRemainingPlannedWeightAsync(CutOrder cutOrder, CancellationToken ct)
    {
        var activeSummary = await _cutOrderRepo.GetActiveExportScaleCutOrdersAsync(
            new ExportScaleCutOrderFilter(cutOrder.ErpCutOrderId, null, null, null, null),
            ct);
        var currentSummary = activeSummary.FirstOrDefault(x => x.CutOrderId == cutOrder.Id);
        return currentSummary != null && currentSummary.RemainingWeight > 0m
            ? currentSummary.RemainingWeight
            : cutOrder.PlannedWeight;
    }

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static void ValidateTransferSourceCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn không thuộc luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến từ cắt lệnh đã chốt.");
        }
    }

    private static void ValidateTransferTargetCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh đích chưa được chuyển sang luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đích đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến sang cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến sang cắt lệnh đã chốt.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh đích không ở trạng thái cân xuất khẩu.");
        }
    }
}

public sealed class DeleteExportVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public DeleteExportVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe cần xóa.");

        if (session.IsDeleted)
        {
            return;
        }

        if (session.Weight2.HasValue || session.Weight2Time.HasValue)
        {
            throw new InvalidOperationException("Không thể xóa chuyến xe đã hoàn thành cân lần 2.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var activeLines = lines.Where(x => !x.IsDeleted).ToList();
        if (activeLines.Count != 1)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ xóa chuyến xuất khẩu có đúng 1 dòng cắt lệnh.");
        }

        var line = activeLines[0];
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh nguồn của chuyến xe.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể xóa chuyến xe thuộc cắt lệnh đã chốt.");
        }

        var weighTickets = (await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var deliveryTickets = (await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var existingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);
        var existingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);

        var now = _clock.NowLocal;
        var username = _userContext.Username;

        session.IsDeleted = true;
        session.IsCancelled = true;
        session.SessionStatus = WeighingSessionStatus.CANCELLED;
        session.DeletedAt = now;
        session.DeletedBy = username;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = username;

        line.IsDeleted = true;
        line.DeletedAt = now;
        line.DeletedBy = username;
        line.LineStatus = WeighingSessionLineStatus.CANCELLED;
        line.ActualAllocatedWeight = null;
        line.ActualAllocatedBagCount = null;
        line.BagCountDisplay = null;
        line.IsReturnedBrokenTrip = false;
        line.DeliveryTicketId = null;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = username;

        foreach (var weighTicket in weighTickets)
        {
            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.NetWeight = 0m;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = username;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.AllocatedWeight = 0m;
            deliveryTicket.AllocatedBagCount = 0;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = username;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = username;
        }

        if (cutOrder.CurrentPrimaryWeighTicketId.HasValue && weighTickets.Any(x => x.Id == cutOrder.CurrentPrimaryWeighTicketId.Value))
        {
            cutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(
                existingWeighTickets.Where(x => weighTickets.All(deleted => deleted.Id != x.Id)))?.Id;
        }

        if (cutOrder.CurrentPrimaryDeliveryTicketId.HasValue && deliveryTickets.Any(x => x.Id == cutOrder.CurrentPrimaryDeliveryTicketId.Value))
        {
            cutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(
                existingDeliveryTickets.Where(x => deliveryTickets.All(deleted => deleted.Id != x.Id)))?.Id;
        }

        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            await _sessionRepo.UpdateLineAsync(line, innerCt);

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
        }, ct);
    }

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }
}

public sealed class FinalizeExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public FinalizeExportCutOrderUseCase(
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(FinalizeExportCutOrderRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng thu\u1ed9c lu\u1ed3ng c\u00e2n xu\u1ea5t kh\u1ea9u.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh \u0111\u00e3 b\u1ecb h\u1ee7y ho\u1eb7c x\u00f3a.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        if (trips.Any(x => x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1
                or WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("Kh\u00f4ng th\u1ec3 ch\u1ed1t khi c\u00f2n chuy\u1ebfn xe d\u1edf dang.");
        }

        var totalWeight = trips
            .Where(x => x.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED)
            .Sum(x => ExportReturnedBrokenTripHelper.ResolveSignedWeight(x.ActualAllocatedWeight, x.IsReturnedBrokenTrip));
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("Ch\u01b0a c\u00f3 chuy\u1ebfn xe h\u1ee3p l\u1ec7 \u0111\u1ec3 ch\u1ed1t s\u1ed1 l\u01b0\u1ee3ng.");
        }

        var now = _clock.NowLocal;
        cutOrder.ExportFinalizedWeight = totalWeight;
        cutOrder.ExportFinalizedAt = now;
        cutOrder.ExportFinalizedBy = _userContext.Username;
        cutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        cutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}

public sealed class ToggleExportReturnedBrokenTripUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public ToggleExportReturnedBrokenTripUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionLineId, bool isReturnedBrokenTrip, CancellationToken ct)
    {
        var line = await _sessionRepo.GetLineByIdAsync(sessionLineId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dòng chuyến xe cần cập nhật.");
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh của chuyến xe.");
        var session = await _sessionRepo.GetByIdAsync(line.WeighingSessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân của chuyến xe.");

        if (line.IsDeleted || cutOrder.IsDeleted || cutOrder.IsCancelled || session.IsDeleted || session.IsCancelled)
        {
            throw new InvalidOperationException("Chuyến xe không còn hợp lệ để cập nhật trạng thái hàng hoàn.");
        }

        if (!cutOrder.IsExportScale || cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ đánh dấu hàng hoàn cho luồng cân xuất khẩu.");
        }

        if ((line.ActualAllocatedWeight ?? 0m) <= 0m)
        {
            throw new InvalidOperationException("Chỉ được đánh dấu hàng hoàn khi chuyến đã có số lượng thực xuất.");
        }

        if (line.IsReturnedBrokenTrip == isReturnedBrokenTrip)
        {
            return;
        }

        var now = _clock.NowLocal;
        line.IsReturnedBrokenTrip = isReturnedBrokenTrip;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateLineAsync(line, innerCt),
            ct);
    }
}
