using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Application.Formatting;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CaptureSessionWeight2UseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IProductRepository _productRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IWeighingSessionImageRepository _imageRepo;
    private readonly ICameraSettingsProvider _cameraSettingsProvider;
    private readonly ICameraCaptureService _cameraCaptureService;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly IToleranceProvider _toleranceProvider;
    private readonly WeighingSessionOverweightService _overweightService;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService? _auditService;

    public CaptureSessionWeight2UseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IProductRepository productRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IWeighingSessionImageRepository imageRepo,
        ICameraSettingsProvider cameraSettingsProvider,
        ICameraCaptureService cameraCaptureService,
        IDeliveryNumberGenerator deliveryNoGen,
        IToleranceProvider toleranceProvider,
        WeighingSessionOverweightService overweightService,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService? auditService = null)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _productRepo = productRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _imageRepo = imageRepo;
        _cameraSettingsProvider = cameraSettingsProvider;
        _cameraCaptureService = cameraCaptureService;
        _deliveryNoGen = deliveryNoGen;
        _toleranceProvider = toleranceProvider;
        _overweightService = overweightService;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditService = auditService;
    }

    public async Task ExecuteAsync(CaptureSessionWeightRequest request, CancellationToken ct)
    {
        EnsureManualPermission(request.Mode);

        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT2 || !session.Weight1.HasValue)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không cho phép lưu cân lần 2.");
        }

        var now = _clock.NowLocal;
        var netWeight = Math.Abs(session.Weight1.Value - request.Weight);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var lineToAutoAllocate = lines.Count == 1 ? lines[0] : null;
        var autoAllocateRegistration = lineToAutoAllocate is null
            ? null
            : registrations.FirstOrDefault(x => x.Id == lineToAutoAllocate.CutOrderId);
        var requiresExportBagCountConfirmation = autoAllocateRegistration != null
            && session.TransactionType == TransactionType.OUTBOUND
            && await IsExportScaleBaggedCutOrderAsync(autoAllocateRegistration, ct);

        if (requiresExportBagCountConfirmation && request.ConfirmedBagCount is null)
        {
            throw new InvalidOperationException("Vui lòng xác nhận số bao thực tế trước khi lưu cân lần 2.");
        }

        if (request.ConfirmedBagCount is < 0)
        {
            throw new InvalidOperationException("Số bao xác nhận không được nhỏ hơn 0.");
        }

        if (session.TransactionType == TransactionType.INBOUND && session.Weight1.Value < request.Weight)
        {
            throw new InvalidOperationException("Phiếu nhập hàng yêu cầu Cân lần 1 phải lớn hơn hoặc bằng Cân lần 2.");
        }

        if (!request.BypassTolerance)
        {
            await ValidateBaggedWeightToleranceAsync(registrations, netWeight, ct);
        }

        session.Weight2 = request.Weight;
        session.Weight2Time = now;
        session.NetWeight = netWeight;
        session.SessionStatus = WeighingSessionStatus.ALLOCATION_PENDING;
        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        var ticket = await _weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, ct)
            ?? throw new InvalidOperationException("Chưa có phiếu cân tổng để cập nhật.");

        var inboundRegistrationsToComplete = new List<CutOrder>();
        var deliveryTicketToCreate = (DeliveryTicket?)null;
        var deliveryTicketToUpdate = (DeliveryTicket?)null;

        if (lineToAutoAllocate != null)
        {
            var registration = autoAllocateRegistration
                ?? registrations.First(x => x.Id == lineToAutoAllocate.CutOrderId);
            var sessionDeliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
            var actualAllocatedWeight = session.NetWeight ?? 0m;
            var actualAllocatedBagCount = requiresExportBagCountConfirmation
                ? request.ConfirmedBagCount
                : WeighingSessionBagCountHelper.ResolveActualBagCount(
                    registration.ProductType,
                    registration.BagCount,
                    lineToAutoAllocate.PlannedBagCount);

            lineToAutoAllocate.ActualAllocatedWeight = actualAllocatedWeight;
            lineToAutoAllocate.ActualAllocatedBagCount = actualAllocatedBagCount;
            lineToAutoAllocate.BagCountDisplay = requiresExportBagCountConfirmation
                ? actualAllocatedBagCount
                : BagCountDisplayHelper.Resolve(
                    actualAllocatedWeight,
                    registration.BagWeightKg,
                    actualAllocatedBagCount);
            if (requiresExportBagCountConfirmation)
            {
                lineToAutoAllocate.SystemCalculatedBagCount = request.SystemCalculatedBagCount;
                lineToAutoAllocate.BagCountConfirmedAt = now;
                lineToAutoAllocate.BagCountConfirmedBy = _userContext.Username;
                lineToAutoAllocate.BagCountConfirmationMode =
                    request.SystemCalculatedBagCount == request.ConfirmedBagCount
                        ? "AcceptedSuggested"
                        : "AdjustedManual";
                ticket.BagCount = actualAllocatedBagCount;
            }
            lineToAutoAllocate.IsReturnedBrokenTrip = request.IsReturnedBrokenTrip;
            lineToAutoAllocate.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            lineToAutoAllocate.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            lineToAutoAllocate.UpdatedAt = now;
            lineToAutoAllocate.UpdatedBy = _userContext.Username;

            var deliveryTicket = sessionDeliveryTickets
                .Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal)
                .FirstOrDefault(x => x.WeighingSessionLineId == lineToAutoAllocate.Id);
            if (deliveryTicket == null)
            {
                deliveryTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = registration.Id,
                    WeighingSessionId = session.Id,
                    WeighingSessionLineId = lineToAutoAllocate.Id,
                    DeliveryNo = string.Empty, // Sẽ sinh trong transaction
                    ErpCutOrderId = registration.ErpCutOrderId ?? string.Empty,
                    CustomerCode = registration.CustomerCode,
                    ProductCode = registration.ProductCode,
                    Notes = registration.Notes,
                    RecordRole = DeliveryTicketRecordRoles.Normal,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                };
                deliveryTicketToCreate = deliveryTicket;
                sessionDeliveryTickets = [.. sessionDeliveryTickets, deliveryTicket];
            }
            else
            {
                deliveryTicketToUpdate = deliveryTicket;
            }

            deliveryTicket.AllocatedWeight = actualAllocatedWeight;
            deliveryTicket.AllocatedBagCount = actualAllocatedBagCount;
            deliveryTicket.IsOverWeight = false;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
            lineToAutoAllocate.DeliveryTicketId = deliveryTicket.Id;

            if (session.TransactionType == TransactionType.INBOUND)
            {
                session.IsOverweight = false;
                session.OverweightAmount = 0m;
                session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
                session.OverweightResolvedAt = null;
                session.OverweightResolvedBy = null;
                session.SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE;

                foreach (var registrationToComplete in registrations)
                {
                    registrationToComplete.CutOrderStatus = CutOrderStatus.COMPLETED;
                    registrationToComplete.ProcessingStage = ProcessingStage.OUT_YARD;
                    registrationToComplete.SyncStatus = SyncStatus.SYNC_QUEUED;
                    registrationToComplete.UpdatedAt = now;
                    registrationToComplete.UpdatedBy = _userContext.Username;
                    inboundRegistrationsToComplete.Add(registrationToComplete);
                }
            }
            else
            {
                _overweightService.RefreshSessionOverweightState(
                    session,
                    lines,
                    [ticket],
                    sessionDeliveryTickets,
                    now,
                    _userContext.Username);

                deliveryTicket.IsOverWeight = session.IsOverweight;
                session.SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE;
            }
        }

        _ticketSyncService.SyncMasterTicketFromSession(
            session,
            ticket,
            now,
            _userContext.Username,
            weight2Snapshot: new WeightCaptureSnapshot(_userContext.Username, request.Mode, request.IsStable));

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            if (deliveryTicketToCreate != null && string.IsNullOrEmpty(deliveryTicketToCreate.DeliveryNo))
            {
                deliveryTicketToCreate.DeliveryNo = await _deliveryNoGen.GenerateAsync(innerCt);
            }

            await _sessionRepo.UpdateAsync(session, innerCt);
            if (lineToAutoAllocate != null)
            {
                await _sessionRepo.UpdateLineAsync(lineToAutoAllocate, innerCt);
                if (deliveryTicketToCreate != null)
                {
                    await _deliveryRepo.AddAsync(deliveryTicketToCreate, innerCt);
                }
                else if (deliveryTicketToUpdate != null)
                {
                    await _deliveryRepo.UpdateAsync(deliveryTicketToUpdate, innerCt);
                }
            }
            foreach (var registrationToComplete in inboundRegistrationsToComplete)
            {
                await _regRepo.UpdateAsync(registrationToComplete, innerCt);
            }
            await _weighRepo.UpdateAsync(ticket, innerCt);
        }, ct);

        await TryCaptureSessionImagesAsync(session.Id, CameraCaptureStage.WEIGHT2, ct);
        await LogManualCaptureAsync(session, request, ct);
    }

    private async Task ValidateBaggedWeightToleranceAsync(
        IReadOnlyList<CutOrder> registrations,
        decimal netWeight,
        CancellationToken ct)
    {
        if (registrations.Count == 0)
        {
            return;
        }

        if (registrations.Any(x => x.IsExportScale))
        {
            return;
        }

        var baggedFlags = new List<bool>(registrations.Count);
        foreach (var registration in registrations)
        {
            baggedFlags.Add(await IsBaggedForToleranceAsync(registration, ct));
        }

        if (baggedFlags.Count == 0 || baggedFlags.Any(x => !x))
        {
            return;
        }

        var plannedWeight = registrations.Sum(x => x.PlannedWeight ?? 0m);
        if (plannedWeight <= 0m)
        {
            return;
        }

        var plannedBagCount = registrations.Sum(x => x.BagCount ?? 0);
        var toleranceKgPerBag = await _toleranceProvider.GetToleranceKgPerBagAsync(ct);
        if (toleranceKgPerBag < 0m)
        {
            toleranceKgPerBag = 0m;
        }

        var toleranceKg = toleranceKgPerBag * plannedBagCount;
        var minimumWeight = plannedWeight - toleranceKg;
        var allowedWeight = plannedWeight + toleranceKg;
        if (netWeight < minimumWeight)
        {
            throw new BaggedWeightToleranceExceededException(
                $"Kh\u1ed1i l\u01b0\u1ee3ng h\u00e0ng {netWeight:N0} kg th\u1ea5p h\u01a1n kh\u1ed1i l\u01b0\u1ee3ng k\u1ebf ho\u1ea1ch {plannedWeight:N0} kg v\u00e0 v\u01b0\u1ee3t dung sai cho ph\u00e9p {toleranceKg:N0} kg ({toleranceKgPerBag:##0.###} kg/bao x {plannedBagCount:N0} bao).");
        }

        if (netWeight > allowedWeight)
        {
            throw new BaggedWeightToleranceExceededException(
                $"Khối lượng hàng {netWeight:N0} kg vượt khối lượng kế hoạch {plannedWeight:N0} kg và vượt dung sai cho phép {toleranceKg:N0} kg ({toleranceKgPerBag:##0.###} kg/bao x {plannedBagCount:N0} bao).");
        }
    }

    private async Task<bool> IsExportScaleBaggedCutOrderAsync(CutOrder registration, CancellationToken ct)
    {
        if (!registration.IsExportScale)
        {
            return false;
        }

        var normalizedPackageType = ExportPackageTypes.Normalize(registration.ExportPackageType);
        if (!string.IsNullOrWhiteSpace(normalizedPackageType))
        {
            return string.Equals(normalizedPackageType, ExportPackageTypes.Bagged, StringComparison.Ordinal);
        }

        if (registration.BagWeightKg.HasValue && registration.BagWeightKg.Value > 0m)
        {
            return true;
        }

        var normalizedProductType = ProductTypes.Normalize(registration.ProductType);
        if (string.IsNullOrWhiteSpace(normalizedProductType)
            && !string.IsNullOrWhiteSpace(registration.ProductCode))
        {
            var product = await _productRepo.GetByCodeAsync(registration.ProductCode.Trim(), ct);
            normalizedProductType = ProductTypes.Normalize(product?.ProductType);
        }

        return string.Equals(normalizedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsBaggedForToleranceAsync(CutOrder registration, CancellationToken ct)
    {
        if (registration.IsExportScale)
        {
            return await IsExportScaleBaggedCutOrderAsync(registration, ct);
        }

        var normalizedProductType = ProductTypes.Normalize(registration.ProductType);
        if (string.IsNullOrWhiteSpace(normalizedProductType)
            && !string.IsNullOrWhiteSpace(registration.ProductCode))
        {
            var product = await _productRepo.GetByCodeAsync(registration.ProductCode.Trim(), ct);
            normalizedProductType = ProductTypes.Normalize(product?.ProductType);
        }

        return string.Equals(normalizedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string?>> ResolveProductTypesAsync(
        IReadOnlyList<CutOrder> registrations,
        CancellationToken ct)
    {
        var resolvedTypes = new string?[registrations.Count];
        var productTypeByCode = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < registrations.Count; i++)
        {
            var registration = registrations[i];
            if (!string.IsNullOrWhiteSpace(registration.ProductType))
            {
                resolvedTypes[i] = registration.ProductType;
                continue;
            }

            if (string.IsNullOrWhiteSpace(registration.ProductCode))
            {
                continue;
            }

            var normalizedCode = registration.ProductCode.Trim();
            if (!productTypeByCode.TryGetValue(normalizedCode, out var productType))
            {
                productType = (await _productRepo.GetByCodeAsync(normalizedCode, ct))?.ProductType;
                productTypeByCode[normalizedCode] = productType;
            }

            resolvedTypes[i] = productType;
        }

        return resolvedTypes;
    }

    private void EnsureManualPermission(WeightMode mode)
    {
        if (mode == WeightMode.MANUAL && !StationAuthorization.CanUseManualWeighing(_userContext.RoleCode))
        {
            throw new InvalidOperationException("Tài khoản hiện tại không có quyền cân tay.");
        }
    }

    private async Task TryCaptureSessionImagesAsync(Guid sessionId, CameraCaptureStage stage, CancellationToken ct)
    {
        try
        {
            var registrations = await _regRepo.GetByWeighingSessionIdAsync(sessionId, ct);
            var isExport = registrations.Any(x => x.IsExportScale);
            var settings = await _cameraSettingsProvider.GetForStationAsync(null, ct);
            if (settings.EnabledCameras.Count == 0)
            {
                return;
            }

            var captures = await _cameraCaptureService.CaptureAsync(
                settings.EnabledCameras,
                settings.CaptureTimeoutMs,
                settings.CaptureJpegQuality,
                settings.CaptureMaxDimension,
                settings.CaptureWarmupFrames,
                ct);

            var successfulCaptures = captures
                .Where(x => x.Success && x.ImageBytes.Length > 0)
                .ToList();
            if (successfulCaptures.Count == 0)
            {
                return;
            }

            var now = _clock.NowLocal;
            await _uow.ExecuteInTransactionAsync(async innerCt =>
            {
                foreach (var capture in successfulCaptures)
                {
                    await _imageRepo.AddAsync(
                        new WeighingSessionImage
                        {
                            Id = Guid.NewGuid(),
                            WeighingSessionId = sessionId,
                            CaptureStage = stage,
                            CameraCode = capture.CameraCode,
                            CameraName = capture.CameraName,
                            RtspUrlSnapshot = capture.RtspUrlSnapshot,
                            ImageFormat = capture.ImageFormat,
                            ImageBytes = capture.ImageBytes,
                            FileSizeBytes = capture.ImageBytes.LongLength,
                            CapturedAt = capture.CapturedAt,
                            CapturedBy = _userContext.Username,
                            CreatedAt = now,
                            CreatedBy = _userContext.Username,
                            UpdatedAt = now,
                            UpdatedBy = _userContext.Username
                        },
                        innerCt);
                }
            }, ct);
        }
        catch
        {
            // Camera capture failures must not fail the weighing flow.
        }
    }

    private async Task LogManualCaptureAsync(
        WeighingSession session,
        CaptureSessionWeightRequest request,
        CancellationToken ct)
    {
        if (request.Mode != WeightMode.MANUAL || _auditService == null)
        {
            return;
        }

        var detail = new AuditLogDetailBuilder()
            .WithSubject(nameof(WeighingSession.SessionNo), session.SessionNo)
            .WithSubject(nameof(WeighingSession.VehiclePlate), session.VehiclePlate)
            .AddChange(nameof(WeighingSession.Weight2), null, request.Weight)
            .WithSummary(nameof(WeighingSession.Weight1), session.Weight1)
            .WithSummary(nameof(WeighingSession.NetWeight), session.NetWeight)
            .AddNote("Người dùng ghi nhận cân tay lần 2.")
            .Build();

        await _auditService.LogAsync("CAPTURE_MANUAL_WEIGHT_2", nameof(WeighingSession), session.Id, detail, ct);
    }
}
