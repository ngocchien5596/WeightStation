using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CaptureSessionWeight1UseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IWeighingSessionImageRepository _imageRepo;
    private readonly ICameraSettingsProvider _cameraSettingsProvider;
    private readonly ICameraCaptureService _cameraCaptureService;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly IIncomingVehicleComplianceSettingsProvider _complianceSettingsProvider;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService? _auditService;

    public CaptureSessionWeight1UseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IVehicleRepository vehicleRepo,
        IWeighTicketRepository weighRepo,
        IWeighingSessionImageRepository imageRepo,
        ICameraSettingsProvider cameraSettingsProvider,
        ICameraCaptureService cameraCaptureService,
        WeighingSessionTicketSyncService ticketSyncService,
        ITicketNumberGenerator ticketNoGen,
        IIncomingVehicleComplianceSettingsProvider complianceSettingsProvider,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService? auditService = null)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _vehicleRepo = vehicleRepo;
        _weighRepo = weighRepo;
        _imageRepo = imageRepo;
        _cameraSettingsProvider = cameraSettingsProvider;
        _cameraCaptureService = cameraCaptureService;
        _ticketSyncService = ticketSyncService;
        _ticketNoGen = ticketNoGen;
        _complianceSettingsProvider = complianceSettingsProvider;
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

        if (session.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không cho phép lưu cân lần 1.");
        }

        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        if (registrations.Count == 0)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không chứa cắt lệnh nào.");
        }

        var primaryRegistration = registrations
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .First();

        var vehicle = (Vehicle?)null;
        if (!string.IsNullOrWhiteSpace(primaryRegistration.VehiclePlate))
        {
            try
            {
                vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(
                    primaryRegistration.VehiclePlate,
                    primaryRegistration.MoocNumber ?? string.Empty,
                    ct)
                    ?? (await _vehicleRepo.GetByPlateAsync(primaryRegistration.VehiclePlate, ct)).FirstOrDefault();
            }
            catch
            {
                // Ignore vehicle lookup errors.
            }
        }

        var ttcp10Threshold = (decimal?)null;
        if (vehicle != null && vehicle.TtcpWeight.HasValue && vehicle.TtcpWeight.Value > 0m)
        {
            ttcp10Threshold = decimal.Round(vehicle.TtcpWeight.Value * 1.10m, 3, MidpointRounding.AwayFromZero);
        }

        var isExportScaleSession = registrations.Any(x => x.IsExportScale);
        var complianceRules = await _complianceSettingsProvider.GetCurrentRulesAsync(ct);
        var normalizedProductType = ProductTypes.Normalize(primaryRegistration.ProductType);
        var requireTtcp = false;
        if (normalizedProductType == ProductTypes.Bagged)
        {
            requireTtcp = complianceRules.BaggedOutbound.RequireTtcpOnCreateSession;
        }
        else if (ProductTypes.IsBulkLike(normalizedProductType))
        {
            requireTtcp = complianceRules.BulkOutbound.RequireTtcpOnCreateSession;
        }

        if (session.TransactionType == TransactionType.OUTBOUND
            && !isExportScaleSession
            && requireTtcp
            && !ttcp10Threshold.HasValue)
        {
            throw new InvalidOperationException(
                $"Xe {session.VehiclePlate}{(string.IsNullOrWhiteSpace(session.MoocNumber) ? string.Empty : $" / mooc {session.MoocNumber}")} chưa có TTCP hợp lệ trong Danh mục xe.");
        }

        var ticket = await _weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, ct);
        var isNewTicket = ticket == null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = _clock.NowLocal;
            if (ticket == null)
            {
                var ticketNo = await _ticketNoGen.GenerateAsync(innerCt);
                ticket = new WeighTicket
                {
                    Id = Guid.NewGuid(),
                    TicketNo = ticketNo,
                    StationCode = primaryRegistration.StationCode,
                    WeighingSessionId = session.Id,
                    CutOrderId = primaryRegistration.Id,
                    ErpCutOrderId = primaryRegistration.ErpCutOrderId,
                    VehiclePlate = primaryRegistration.VehiclePlate,
                    MoocNumber = primaryRegistration.MoocNumber,
                    DriverName = primaryRegistration.ReceiverName,
                    CustomerCode = primaryRegistration.CustomerCode,
                    CustomerName = primaryRegistration.CustomerName,
                    ProductCode = primaryRegistration.ProductCode,
                    ProductName = primaryRegistration.ProductName,
                    PlannedWeight = registrations.Sum(x => x.PlannedWeight ?? 0m),
                    BagCount = registrations.Sum(x => x.BagCount ?? 0),
                    Notes = registrations.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Notes))?.Notes,
                    TransactionType = session.TransactionType,
                    TransportMethod = primaryRegistration.TransportMethod,
                    Status = TicketStatus.LOADING_STARTED,
                    RecordRole = WeighTicketRecordRoles.MasterSession,
                    IsPrimaryDisplay = true,
                    Ttcp10WeightSnapshot = ttcp10Threshold,
                    IdempotencyKey = Guid.NewGuid(),
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username
                };
            }

            session.Weight1 = request.Weight;
            session.Weight1Time = now;
            session.Ttcp10WeightSnapshot = ttcp10Threshold;
            session.SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2;
            session.IsOverweight = false;
            session.OverweightAmount = 0m;
            session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
            session.OverweightResolvedAt = null;
            session.OverweightResolvedBy = null;
            session.UpdatedAt = now;
            session.UpdatedBy = _userContext.Username;

            var masterTicket = ticket ?? throw new InvalidOperationException("Không thể khởi tạo phiếu cân tổng.");
            masterTicket.VehicleRegistrationNoSnapshot = vehicle?.VehicleRegistrationNo;
            masterTicket.VehicleRegistrationExpirySnapshot = vehicle?.VehicleRegistrationExpiryDate;
            masterTicket.MoocRegistrationNoSnapshot = vehicle?.MoocRegistrationNo;
            masterTicket.MoocRegistrationExpirySnapshot = vehicle?.MoocRegistrationExpiryDate;
            _ticketSyncService.SyncMasterTicketFromSession(
                session,
                masterTicket,
                now,
                _userContext.Username,
                new WeightCaptureSnapshot(_userContext.Username, request.Mode, request.IsStable));

            await _sessionRepo.UpdateAsync(session, innerCt);
            if (isNewTicket)
            {
                await _weighRepo.AddAsync(masterTicket, innerCt);
            }
            else
            {
                await _weighRepo.UpdateAsync(masterTicket, innerCt);
            }
        }, ct);

        await TryCaptureSessionImagesAsync(session.Id, CameraCaptureStage.WEIGHT1, ct);
        await LogManualCaptureAsync(session, primaryRegistration, request, ct);
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
                            StationCode = _userContext.StationCode,
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
        CutOrder primaryRegistration,
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
            .AddChange(nameof(WeighingSession.Weight1), null, request.Weight)
            .WithSummary(nameof(CutOrder.ErpCutOrderId), primaryRegistration.ErpCutOrderId)
            .AddNote("Người dùng ghi nhận cân tay lần 1.")
            .Build();

        await _auditService.LogAsync("CAPTURE_MANUAL_WEIGHT_1", nameof(WeighingSession), session.Id, detail, ct);
    }
}
