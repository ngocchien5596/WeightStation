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
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IWeighingSessionNumberGenerator _sessionNoGen;

    public CreateWeighingSessionUseCase(
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

    public async Task<CreateWeighingSessionResult> ExecuteAsync(CreateWeighingSessionRequest request, CancellationToken ct)
    {
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

public sealed class CaptureSessionWeight1UseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IWeighTicketRepository _weighRepo;
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
        var ttcp10Threshold = session.Ttcp10WeightSnapshot
            ?? decimal.Round((vehicle?.TtcpWeight ?? lines.Sum(x => x.PlannedWeight ?? 0m)) * 1.10m, 3, MidpointRounding.AwayFromZero);

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
    }

    private void EnsureManualPermission(WeightMode mode)
    {
        if (mode == WeightMode.MANUAL && !StationAuthorization.CanUseManualWeighing(_userContext.RoleCode))
        {
            throw new InvalidOperationException("Tài khoản hiện tại không có quyền cân tay.");
        }
    }
}

public sealed class CaptureSessionWeight2UseCase
{
    private const decimal DefaultBagWeightKg = 50m;

    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
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
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
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
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
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

        await ValidateBaggedWeightToleranceAsync(registrations, netWeight, ct);

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
        var deliveryTicketToCreate = (DeliveryTicket?)null;
        var deliveryTicketToUpdate = (DeliveryTicket?)null;

        if (lineToAutoAllocate != null)
        {
            var registration = registrations.First(x => x.Id == lineToAutoAllocate.CutOrderId);
            var sessionDeliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
            var actualAllocatedWeight = session.NetWeight ?? 0m;
            var actualAllocatedBagCount = CalculateAutoBagCount(actualAllocatedWeight);

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
            await _weighRepo.UpdateAsync(ticket, innerCt);
        }, ct);
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

        var normalizedTypes = registrations
            .Select(x => ProductTypes.Normalize(x.ProductType))
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

        var toleranceKg = await _toleranceProvider.GetToleranceKgAsync(ct);
        if (toleranceKg < 0m)
        {
            toleranceKg = 0m;
        }

        var allowedWeight = plannedWeight + toleranceKg;
        if (netWeight > allowedWeight)
        {
            throw new InvalidOperationException(
                $"Khối lượng hàng {netWeight:N0} kg vượt khối lượng kế hoạch {plannedWeight:N0} kg và vượt dung sai cho phép {toleranceKg:N0} kg.");
        }
    }

    private static int CalculateAutoBagCount(decimal actualWeight)
    {
        if (actualWeight <= 0m)
        {
            return 0;
        }

        return (int)decimal.Round(actualWeight / DefaultBagWeightKg, 0, MidpointRounding.AwayFromZero);
    }

    private void EnsureManualPermission(WeightMode mode)
    {
        if (mode == WeightMode.MANUAL && !StationAuthorization.CanUseManualWeighing(_userContext.RoleCode))
        {
            throw new InvalidOperationException("Tài khoản hiện tại không có quyền cân tay.");
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
        var inputByLineId = request.Lines.ToDictionary(x => x.SessionLineId);
        var ticketsToCreate = new List<DeliveryTicket>();

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
            lines.Count(line => !deliveryTicketByLineId.ContainsKey(line.Id)),
            ct));

        var now = _clock.NowLocal;
        foreach (var line in lines)
        {
            var input = inputByLineId[line.Id];
            line.ActualAllocatedWeight = input.ActualAllocatedWeight;
            line.ActualAllocatedBagCount = input.ActualAllocatedBagCount;
            line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;

            var registration = registrations.First(x => x.Id == line.CutOrderId);
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
            deliveryTicket.AllocatedBagCount = input.ActualAllocatedBagCount;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
            line.DeliveryTicketId = deliveryTicket.Id;
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

        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);
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

            foreach (var ticket in deliveryTicketByLineId.Values)
            {
                if (!ticketsToCreate.Contains(ticket))
                {
                    await _deliveryRepo.UpdateAsync(ticket, innerCt);
                }
            }

            foreach (var ticket in sessionDeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived))
            {
                await _deliveryRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession
                                                        || x.RecordRole == WeighTicketRecordRoles.SplitDerived))
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
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

        var firstNumber = await _deliveryNoGen.GenerateAsync(ct);
        if (count == 1)
        {
            return [firstNumber];
        }

        var splitIndex = firstNumber.Length;
        while (splitIndex > 0 && char.IsDigit(firstNumber[splitIndex - 1]))
        {
            splitIndex--;
        }

        var prefix = firstNumber[..splitIndex];
        var numericPart = firstNumber[splitIndex..];
        if (numericPart.Length == 0 || !int.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out var startSequence))
        {
            return Enumerable.Range(0, count)
                .Select(offset => offset == 0 ? firstNumber : $"{firstNumber}-{offset + 1}")
                .ToList();
        }

        return Enumerable.Range(0, count)
            .Select(offset => $"{prefix}{(startSequence + offset).ToString($"D{numericPart.Length}", CultureInfo.InvariantCulture)}")
            .ToList();
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

        if (session.SessionStatus is WeighingSessionStatus.COMPLETED or WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không thể chuyển xe ra theo luồng không lấy hàng.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var now = _clock.NowLocal;
        var referenceWeight = session.Weight2 ?? session.Weight1 ?? 0m;

        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.Weight1 = referenceWeight;
        session.Weight1Time ??= now;
        session.Weight2 = referenceWeight;
        session.Weight2Time = now;
        session.NetWeight = 0m;
        session.Ttcp10WeightSnapshot ??= 0m;
        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
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
            registration.CutOrderStatus = CutOrderStatus.COMPLETED;
            registration.ProcessingStage = ProcessingStage.OUT_YARD;
            registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            registration.CurrentPrimaryWeighTicketId = null;
            registration.CurrentPrimaryDeliveryTicketId = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);
        if (masterWeighTicket != null)
        {
            _ticketSyncService.SyncMasterTicketFromSession(session, masterWeighTicket, now, _userContext.Username);
        }

        foreach (var weighTicket in weighTickets)
        {
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
            registration.CutOrderStatus = CutOrderStatus.COMPLETED;
            registration.ProcessingStage = ProcessingStage.OUT_YARD;
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
            registration.CutOrderStatus = CutOrderStatus.REGISTERED;
            registration.ProcessingStage = ProcessingStage.IN_YARD;
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

    public Task<IReadOnlyList<WeighingSessionListItem>> ExecuteAsync(string? keyword, CancellationToken ct)
    {
        return _sessionRepo.SearchActiveSessionsAsync(keyword, ct);
    }
}




