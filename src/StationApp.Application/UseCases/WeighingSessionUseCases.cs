using System.Globalization;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
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

        var now = _clock.NowLocal;
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = await _sessionNoGen.GenerateAsync(primaryRegistration.TransactionType, ct),
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

        var orderedRegistrations = registrations
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .ToList();

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

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.AddAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.AddLineAsync(line, innerCt);
            }

            foreach (var registration in orderedRegistrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }
        }, ct);

        return new CreateWeighingSessionResult(session.Id);
    }
}

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
        var primaryRegistration = request.PrimaryCutOrderId.HasValue
            ? registrations.FirstOrDefault(x => x.Id == request.PrimaryCutOrderId.Value)
            : null;
        primaryRegistration ??= first;

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
                throw new InvalidOperationException("Không thể xử lý nhiều cắt lệnh khác loại trong cùng một lượt xe ra.");
            }
        }

        var now = _clock.NowLocal;
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = await _sessionNoGen.GenerateAsync(primaryRegistration.TransactionType, ct),
            TransactionType = primaryRegistration.TransactionType,
            VehiclePlate = primaryRegistration.VehiclePlate,
            MoocNumber = primaryRegistration.MoocNumber,
            DriverName = primaryRegistration.ReceiverName,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 0m,
            Weight1Time = now,
            Weight2 = 0m,
            Weight2Time = now,
            NetWeight = 0m,
            IsOverweight = false,
            OverweightAmount = 0m,
            OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE,
            IsCancelled = false,
            HasPrintedMasterWeighTicket = false,
            CreatedAt = now,
            CreatedBy = _userContext.Username,
            UpdatedAt = now,
            UpdatedBy = _userContext.Username
        };

        var orderedRegistrations = registrations
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .ToList();

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
            ActualAllocatedWeight = 0m,
            ActualAllocatedBagCount = 0,
            LineStatus = WeighingSessionLineStatus.ALLOCATED,
            HasPrintedDeliveryTicket = false,
            CreatedAt = now,
            CreatedBy = _userContext.Username,
            UpdatedAt = now,
            UpdatedBy = _userContext.Username
        }).ToList();

        foreach (var registration in orderedRegistrations)
        {
            registration.CutOrderStatus = CutOrderStatus.COMPLETED;
            registration.ProcessingStage = ProcessingStage.OUT_YARD;
            registration.WeighingSessionId = session.Id;
            registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.AddAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.AddLineAsync(line, innerCt);
            }

            foreach (var registration in orderedRegistrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }
        }, ct);

        return session.Id;
    }
}

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

public sealed class SetWeighingSessionBaggedActualWeightOverrideUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public SetWeighingSessionBaggedActualWeightOverrideUseCase(
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, bool enabled, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.UseActualWeightForBaggedCutOrders == enabled)
        {
            return;
        }

        session.UseActualWeightForBaggedCutOrders = enabled;
        session.UpdatedAt = _clock.NowLocal;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateAsync(session, innerCt),
            ct);
    }
}

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
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

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
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
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
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
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
        var primaryRegistration = registrations.OrderBy(x => x.CreatedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Lượt cân chưa có dòng cắt lệnh.");

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, session.MoocNumber ?? string.Empty, ct)
            ?? (await _vehicleRepo.GetByPlateAsync(session.VehiclePlate, ct)).FirstOrDefault();
        var vehicleTtcpWeight = vehicle?.TtcpWeight;
        var ttcp10Threshold = session.Ttcp10WeightSnapshot;
        if (!ttcp10Threshold.HasValue && vehicleTtcpWeight.HasValue && vehicleTtcpWeight.Value > 0m)
        {
            ttcp10Threshold = decimal.Round(vehicleTtcpWeight.Value * 1.10m, 3, MidpointRounding.AwayFromZero);
        }

        if (session.TransactionType == TransactionType.OUTBOUND && !ttcp10Threshold.HasValue)
        {
            throw new InvalidOperationException(
                $"Xe {session.VehiclePlate}{(string.IsNullOrWhiteSpace(session.MoocNumber) ? string.Empty : $" / mooc {session.MoocNumber}")} chưa có TTCP hợp lệ trong Danh mục xe.");
        }

        var ticket = await _weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, ct);
        var isNewTicket = ticket == null;
        var now = _clock.NowLocal;

        if (isNewTicket)
        {
            ticket = new WeighTicket
            {
                Id = Guid.NewGuid(),
                CutOrderId = primaryRegistration.Id,
                WeighingSessionId = session.Id,
                TicketNo = await _ticketNoGen.GenerateAsync(ct),
                ErpCutOrderId = primaryRegistration.ErpCutOrderId,
                VehiclePlate = session.VehiclePlate,
                MoocNumber = session.MoocNumber,
                DriverName = session.DriverName,
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

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
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
            var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? "C6" : "C2", ct);
            if (settings.EnabledCameras.Count == 0)
            {
                return;
            }

            var captures = await _cameraCaptureService.CaptureAsync(
                settings.EnabledCameras,
                settings.CaptureTimeoutMs,
                settings.CaptureJpegQuality,
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
}

internal static class WeighingSessionBagCountHelper
{
    public static int? ResolveActualBagCount(
        string? productType,
        int? registrationBagCount,
        int? plannedBagCount,
        int? fallbackBagCount = null)
    {
        if (!string.Equals(ProductTypes.Normalize(productType), ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase))
        {
            return fallbackBagCount;
        }

        return registrationBagCount ?? plannedBagCount ?? fallbackBagCount;
    }
}

public class BaggedWeightToleranceExceededException : InvalidOperationException
{
    public BaggedWeightToleranceExceededException(string message) : base(message)
    {
    }
}

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
        IClock clock)
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

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var lineToAutoAllocate = lines.Count == 1 ? lines[0] : null;
        var inboundRegistrationsToComplete = new List<CutOrder>();
        var deliveryTicketToCreate = (DeliveryTicket?)null;
        var deliveryTicketToUpdate = (DeliveryTicket?)null;

        if (lineToAutoAllocate != null)
        {
            var registration = registrations.First(x => x.Id == lineToAutoAllocate.CutOrderId);
            var sessionDeliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
            var actualAllocatedWeight = session.NetWeight ?? 0m;
            var actualAllocatedBagCount = WeighingSessionBagCountHelper.ResolveActualBagCount(
                registration.ProductType,
                registration.BagCount,
                lineToAutoAllocate.PlannedBagCount);

            lineToAutoAllocate.ActualAllocatedWeight = actualAllocatedWeight;
            lineToAutoAllocate.ActualAllocatedBagCount = actualAllocatedBagCount;
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
                    DeliveryNo = await _deliveryNoGen.GenerateAsync(ct),
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

        var normalizedTypes = (await ResolveProductTypesAsync(registrations, ct))
            .Select(ProductTypes.Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTypes.Count != 1
            || !string.Equals(normalizedTypes[0], ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var plannedWeight = registrations.Sum(x => x.PlannedWeight ?? 0m);
        if (plannedWeight <= 0m || netWeight <= plannedWeight)
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
        var allowedWeight = plannedWeight + toleranceKg;
        if (netWeight > allowedWeight)
        {
            throw new BaggedWeightToleranceExceededException(
                $"Khối lượng hàng {netWeight:N0} kg vượt khối lượng kế hoạch {plannedWeight:N0} kg và vượt dung sai cho phép {toleranceKg:N0} kg ({toleranceKgPerBag:##0.###} kg/bao x {plannedBagCount:N0} bao).");
        }
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
            var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? "C6" : "C2", ct);
            if (settings.EnabledCameras.Count == 0)
            {
                return;
            }

            var captures = await _cameraCaptureService.CaptureAsync(
                settings.EnabledCameras,
                settings.CaptureTimeoutMs,
                settings.CaptureJpegQuality,
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
}

public sealed class AllocateWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly WeighingSessionOverweightService _overweightService;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public AllocateWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IDeliveryNumberGenerator deliveryNoGen,
        ITicketNumberGenerator ticketNoGen,
        WeighingSessionOverweightService overweightService,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _deliveryNoGen = deliveryNoGen;
        _ticketNoGen = ticketNoGen;
        _overweightService = overweightService;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(AllocateWeighingSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if ((session.SessionStatus != WeighingSessionStatus.ALLOCATION_PENDING
             && session.SessionStatus != WeighingSessionStatus.READY_TO_COMPLETE)
            || !session.NetWeight.HasValue)
        {
            throw new InvalidOperationException("Lượt cân hiện tại chưa sẵn sàng để phân bổ.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var sessionDeliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTicketByLineId = sessionDeliveryTickets
            .Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal)
            .Where(x => x.WeighingSessionLineId.HasValue)
            .ToDictionary(x => x.WeighingSessionLineId!.Value);
        var deliveryMasterTicket = sessionDeliveryTickets
            .FirstOrDefault(x => x.RecordRole == DeliveryTicketRecordRoles.Master && !x.IsDeleted);
        var lineWeighTicketsByCutOrderId = weighTickets
            .Where(x => x.RecordRole == WeighTicketRecordRoles.CutOrderDerived)
            .ToDictionary(x => x.CutOrderId);
        var inputByLineId = request.Lines.ToDictionary(x => x.SessionLineId);
        var inputOrderByLineId = request.Lines
            .Select((line, index) => new { line.SessionLineId, Index = index })
            .ToDictionary(x => x.SessionLineId, x => x.Index);
        var ticketsToCreate = new List<DeliveryTicket>();
        var weighTicketsToCreate = new List<WeighTicket>();
        var registrationById = registrations.ToDictionary(x => x.Id);

        var totalAllocated = 0m;
        foreach (var line in lines)
        {
            if (!inputByLineId.TryGetValue(line.Id, out var input))
            {
                throw new InvalidOperationException("Thiếu dữ liệu phân bổ cho một hoặc nhiều dòng.");
            }

            totalAllocated += input.ActualAllocatedWeight ?? 0m;
        }

        if (totalAllocated != session.NetWeight.Value)
        {
            throw new InvalidOperationException("Tổng khối lượng phân bổ phải đúng bằng khối lượng thực cân của lượt xe.");
        }

        var nextDeliveryNumbers = new Queue<string>(await AllocateDeliveryNumbersAsync(
            lines.Count(line => !deliveryTicketByLineId.ContainsKey(line.Id))
                + (lines.Count > 1 && deliveryMasterTicket == null ? 1 : 0),
            ct));
        var nextWeighTicketNumbers = new Queue<string>(await AllocateWeighTicketNumbersAsync(
            lines.Count(line =>
            {
                var registration = registrationById[line.CutOrderId];
                return !lineWeighTicketsByCutOrderId.ContainsKey(registration.Id);
            }),
            ct));

        var now = _clock.NowLocal;
        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);
        var lineTicketStartWeight = session.Weight1 ?? 0m;
        if (lines.Count > 1)
        {
            var primaryLine = lines.OrderBy(x => x.SequenceNo).First();
            var primaryRegistration = registrationById[primaryLine.CutOrderId];
            if (deliveryMasterTicket == null)
            {
                deliveryMasterTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = primaryRegistration.Id,
                    WeighingSessionId = session.Id,
                    DeliveryNo = nextDeliveryNumbers.Dequeue(),
                    ErpCutOrderId = primaryRegistration.ErpCutOrderId ?? string.Empty,
                    CustomerCode = primaryRegistration.CustomerCode,
                    ProductCode = primaryRegistration.ProductCode,
                    Notes = primaryRegistration.Notes,
                    RecordRole = DeliveryTicketRecordRoles.Master,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                };
                ticketsToCreate.Add(deliveryMasterTicket);
            }

            deliveryMasterTicket.AllocatedWeight = session.NetWeight;
            deliveryMasterTicket.AllocatedBagCount = request.Lines.Sum(x => x.ActualAllocatedBagCount ?? 0);
            deliveryMasterTicket.UpdatedAt = now;
            deliveryMasterTicket.UpdatedBy = _userContext.Username;
        }
        foreach (var line in lines.OrderBy(x => inputOrderByLineId.GetValueOrDefault(x.Id, int.MaxValue)))
        {
            var input = inputByLineId[line.Id];
            var registration = registrationById[line.CutOrderId];
            line.ActualAllocatedWeight = input.ActualAllocatedWeight;
            line.ActualAllocatedBagCount = WeighingSessionBagCountHelper.ResolveActualBagCount(
                registration.ProductType,
                registration.BagCount,
                line.PlannedBagCount,
                input.ActualAllocatedBagCount);
            line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;

            var deliveryTicket = deliveryTicketByLineId.GetValueOrDefault(line.Id);
            if (deliveryTicket == null)
            {
                deliveryTicket = new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    CutOrderId = registration.Id,
                    WeighingSessionId = session.Id,
                    WeighingSessionLineId = line.Id,
                    DeliveryNo = nextDeliveryNumbers.Dequeue(),
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
                ticketsToCreate.Add(deliveryTicket);
                deliveryTicketByLineId[line.Id] = deliveryTicket;
            }

            deliveryTicket.AllocatedWeight = input.ActualAllocatedWeight;
            deliveryTicket.AllocatedBagCount = line.ActualAllocatedBagCount;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
            line.DeliveryTicketId = deliveryTicket.Id;

            if (lines.Count > 1)
            {
                var lineWeighTicket = lineWeighTicketsByCutOrderId.GetValueOrDefault(registration.Id);
                if (lineWeighTicket == null)
                {
                    lineWeighTicket = new WeighTicket
                    {
                        Id = Guid.NewGuid(),
                        TicketNo = nextWeighTicketNumbers.Dequeue(),
                        IdempotencyKey = Guid.NewGuid(),
                        RecordRole = WeighTicketRecordRoles.CutOrderDerived,
                        CreatedAt = now,
                        CreatedBy = _userContext.Username,
                        Weight1User = session.Weight1Time.HasValue ? _userContext.Username : null,
                        Weight1UpdatedAt = session.Weight1Time.HasValue ? now : null,
                        Weight2User = session.Weight2Time.HasValue ? _userContext.Username : null,
                        Weight2UpdatedAt = session.Weight2Time.HasValue ? now : null
                    };
                    weighTicketsToCreate.Add(lineWeighTicket);
                    lineWeighTicketsByCutOrderId[registration.Id] = lineWeighTicket;
                }

                _ticketSyncService.SyncLineTicketFromSession(session, line, registration, lineWeighTicket, lineTicketStartWeight, now, _userContext.Username);
                if (masterWeighTicket != null)
                {
                    lineWeighTicket.VehicleRegistrationNoSnapshot = masterWeighTicket.VehicleRegistrationNoSnapshot;
                    lineWeighTicket.VehicleRegistrationExpirySnapshot = masterWeighTicket.VehicleRegistrationExpirySnapshot;
                    lineWeighTicket.MoocRegistrationNoSnapshot = masterWeighTicket.MoocRegistrationNoSnapshot;
                    lineWeighTicket.MoocRegistrationExpirySnapshot = masterWeighTicket.MoocRegistrationExpirySnapshot;
                    lineWeighTicket.Weight1Mode = masterWeighTicket.Weight1Mode;
                    lineWeighTicket.Weight1IsStable = masterWeighTicket.Weight1IsStable;
                    lineWeighTicket.Weight2Mode = masterWeighTicket.Weight2Mode;
                    lineWeighTicket.Weight2IsStable = masterWeighTicket.Weight2IsStable;
                }

                registration.CurrentPrimaryWeighTicketId = lineWeighTicket.Id;
                registration.UpdatedAt = now;
                registration.UpdatedBy = _userContext.Username;
                lineTicketStartWeight = lineWeighTicket.Weight2 ?? lineTicketStartWeight;
            }
            else
            {
                registration.CurrentPrimaryWeighTicketId = masterWeighTicket?.Id;
            }
        }

        _overweightService.RefreshSessionOverweightState(
            session,
            lines,
            weighTickets,
            sessionDeliveryTickets,
            now,
            _userContext.Username);

        foreach (var deliveryTicket in deliveryTicketByLineId.Values)
        {
            deliveryTicket.IsOverWeight = session.IsOverweight;
        }

        if (lines.Count == 1)
        {
            if (deliveryMasterTicket != null && !deliveryMasterTicket.IsDeleted)
            {
                deliveryMasterTicket.IsDeleted = true;
                deliveryMasterTicket.DeletedAt = now;
                deliveryMasterTicket.DeletedBy = _userContext.Username;
                deliveryMasterTicket.UpdatedAt = now;
                deliveryMasterTicket.UpdatedBy = _userContext.Username;
            }

            foreach (var extraWeighTicket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.CutOrderDerived && !x.IsDeleted))
            {
                extraWeighTicket.IsDeleted = true;
                extraWeighTicket.DeletedAt = now;
                extraWeighTicket.DeletedBy = _userContext.Username;
                extraWeighTicket.UpdatedAt = now;
                extraWeighTicket.UpdatedBy = _userContext.Username;
            }
        }

        if (masterWeighTicket != null)
        {
            _ticketSyncService.SyncMasterTicketFromSession(session, masterWeighTicket, now, _userContext.Username);
        }

        session.SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var ticket in ticketsToCreate)
            {
                await _deliveryRepo.AddAsync(ticket, innerCt);
            }

            foreach (var ticket in weighTicketsToCreate)
            {
                await _weighRepo.AddAsync(ticket, innerCt);
            }

            foreach (var ticket in deliveryTicketByLineId.Values)
            {
                if (!ticketsToCreate.Contains(ticket))
                {
                    await _deliveryRepo.UpdateAsync(ticket, innerCt);
                }
            }

            if (deliveryMasterTicket != null && !ticketsToCreate.Contains(deliveryMasterTicket))
            {
                await _deliveryRepo.UpdateAsync(deliveryMasterTicket, innerCt);
            }

            foreach (var ticket in lineWeighTicketsByCutOrderId.Values)
            {
                if (!weighTicketsToCreate.Contains(ticket))
                {
                    await _weighRepo.UpdateAsync(ticket, innerCt);
                }
            }

            foreach (var ticket in sessionDeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived))
            {
                await _deliveryRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession
                                                        || x.RecordRole == WeighTicketRecordRoles.CutOrderDerived
                                                        || x.RecordRole == WeighTicketRecordRoles.SplitDerived))
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            await _sessionRepo.UpdateAsync(session, innerCt);
        }, ct);
    }

    private async Task<IReadOnlyList<string>> AllocateDeliveryNumbersAsync(int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        if (count == 1)
        {
            return [await _deliveryNoGen.GenerateAsync(ct)];
        }

        var numbers = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            numbers.Add(await _deliveryNoGen.GenerateAsync(ct));
        }

        return numbers;
    }

    private async Task<IReadOnlyList<string>> AllocateWeighTicketNumbersAsync(int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        if (count == 1)
        {
            return [await _ticketNoGen.GenerateAsync(ct)];
        }

        var numbers = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            numbers.Add(await _ticketNoGen.GenerateAsync(ct));
        }

        return numbers;
    }
}

public sealed class MarkWeighingSessionNoLoadUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public MarkWeighingSessionNoLoadUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(MarkWeighingSessionNoLoadRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không thể chuyển xe ra theo luồng không lấy hàng.");
        }

        if (session.IsNoLoad)
        {
            return;
        }

        if (session.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1 or WeighingSessionStatus.PENDING_WEIGHT2)
        {
            throw new InvalidOperationException("Phải lưu cân lần 2 trước khi đánh dấu không lấy hàng.");
        }

        if (!session.Weight2.HasValue)
        {
            throw new InvalidOperationException("Lượt cân hiện tại chưa có số cân lần 2.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var now = _clock.NowLocal;
        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);

        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.NetWeight = 0m;
        session.Ttcp10WeightSnapshot ??= 0m;
        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
        session.IsNoLoad = true;
        session.HasPrintedMasterWeighTicket = false;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        foreach (var line in lines)
        {
            line.ActualAllocatedWeight = 0m;
            line.ActualAllocatedBagCount = 0;
            line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            line.HasPrintedDeliveryTicket = false;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;
        }

        foreach (var registration in registrations)
        {
            if (registration.IsExportScale)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.WeighingSessionId = null;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }
            else
            {
                registration.CutOrderStatus = CutOrderStatus.COMPLETED;
                registration.ProcessingStage = ProcessingStage.OUT_YARD;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }

            registration.CurrentPrimaryWeighTicketId = masterWeighTicket?.Id;
            registration.CurrentPrimaryDeliveryTicketId = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        if (masterWeighTicket != null)
        {
            _ticketSyncService.SyncMasterTicketFromSession(session, masterWeighTicket, now, _userContext.Username);
            masterWeighTicket.IsDeleted = false;
            masterWeighTicket.IsCancelled = false;
            masterWeighTicket.Status = TicketStatus.TICKET_COMPLETED;
            masterWeighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            masterWeighTicket.DeletedAt = null;
            masterWeighTicket.DeletedBy = null;
            masterWeighTicket.UpdatedAt = now;
            masterWeighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var weighTicket in weighTickets)
        {
            if (masterWeighTicket != null && weighTicket.Id == masterWeighTicket.Id)
            {
                continue;
            }

            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.NetWeight = 0m;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = _userContext.Username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.AllocatedWeight = 0m;
            deliveryTicket.AllocatedBagCount = 0;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = _userContext.Username;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }
        }, ct);
    }
}

public sealed class CompleteWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CompleteWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public static bool CanMoveToOutYard(WeighingSession? session)
    {
        return session != null
            && !session.IsCancelled
            && session.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
            && session.Weight1.HasValue
            && session.Weight2.HasValue
            && session.NetWeight.HasValue
            && session.OverweightResolutionStatus is OverweightResolutionStatus.NOT_APPLICABLE
                or OverweightResolutionStatus.SPLIT_CONFIRMED
                or OverweightResolutionStatus.NO_SPLIT_CONFIRMED;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (!CanMoveToOutYard(session))
        {
            throw new InvalidOperationException("Lượt xe chưa đủ điều kiện để chuyển ra.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(sessionId, ct);
        if (lines.Count == 0
            || lines.Any(x => x.LineStatus != WeighingSessionLineStatus.ALLOCATED)
            || lines.Any(x => !x.ActualAllocatedWeight.HasValue))
        {
            throw new InvalidOperationException("Lượt xe chưa hoàn tất cân hoặc chưa phân bổ xong.");
        }

        var registrations = await _regRepo.GetByWeighingSessionIdAsync(sessionId, ct);
        var now = _clock.NowLocal;

        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        foreach (var registration in registrations)
        {
            if (registration.IsExportScale)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.WeighingSessionId = null;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }
            else
            {
                registration.CutOrderStatus = CutOrderStatus.COMPLETED;
                registration.ProcessingStage = ProcessingStage.OUT_YARD;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }

            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }
        }, ct);
    }
}

public sealed class CancelWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CancelWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(CancelWeighingSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.SessionStatus == WeighingSessionStatus.COMPLETED)
        {
            throw new InvalidOperationException("Lượt cân đã hoàn tất, không thể hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var now = _clock.NowLocal;

        session.SessionStatus = WeighingSessionStatus.CANCELLED;
        session.IsCancelled = true;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        foreach (var line in lines)
        {
            line.LineStatus = WeighingSessionLineStatus.CANCELLED;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;
        }

        foreach (var registration in registrations)
        {
            if (registration.IsExportScale)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }
            else
            {
                registration.CutOrderStatus = CutOrderStatus.REGISTERED;
                registration.ProcessingStage = ProcessingStage.IN_YARD;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }

            registration.WeighingSessionId = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        foreach (var weighTicket in weighTickets)
        {
            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = _userContext.Username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = _userContext.Username;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }
        }, ct);
    }
}

public sealed class GetWeighingSessionsUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;

    public GetWeighingSessionsUseCase(IWeighingSessionRepository sessionRepo)
    {
        _sessionRepo = sessionRepo;
    }

    public Task<IReadOnlyList<WeighingSessionListItem>> ExecuteAsync(string? keyword, TransactionType? transactionType, CancellationToken ct)
    {
        return _sessionRepo.SearchActiveSessionsAsync(keyword, transactionType, ct);
    }
}




