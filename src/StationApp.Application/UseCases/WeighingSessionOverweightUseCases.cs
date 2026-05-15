using System.Globalization;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class PreviewWeighingSessionOverweightSplitUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IAppConfigRepository _configRepo;
    private readonly WeighingSessionOverweightService _overweightService;

    public PreviewWeighingSessionOverweightSplitUseCase(
        IWeighingSessionRepository sessionRepo,
        IAppConfigRepository configRepo,
        WeighingSessionOverweightService overweightService)
    {
        _sessionRepo = sessionRepo;
        _configRepo = configRepo;
        _overweightService = overweightService;
    }

    public Task<OverweightSplitPreviewDto> ExecuteAsync(Guid sessionId, CancellationToken ct)
        => ExecuteAsync(new PreviewWeighingSessionOverweightSplitRequest(sessionId), ct);

    public async Task<OverweightSplitPreviewDto> ExecuteAsync(PreviewWeighingSessionOverweightSplitRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Khong tim thay luot can.");
        var lines = await _sessionRepo.GetLinesBySessionIdAsync(request.SessionId, ct);

        EnsureSessionPendingOverweight(session);

        var splitStepWeight = await ResolveWeighingSessionOverweightSplitUseCase.ResolveSplitStepWeightAsync(_configRepo, ct);
        var plan = _overweightService.BuildSplitPlan(
            session,
            lines,
            splitStepWeight,
            request.FirstSplitNetWeight,
            request.IsManualOverride);

        var previewGroups = plan.Groups
            .Select(x => new OverweightSplitPreviewGroupItem(
                x.GroupId,
                x.SplitSequence,
                x.GroupWeight,
                x.Lines.Count))
            .ToList();

        var lineItems = await _sessionRepo.GetLineItemsBySessionIdAsync(request.SessionId, ct);
        var lineLookup = lineItems.ToDictionary(x => x.SessionLineId);
        var previewLines = plan.Groups
            .SelectMany(group => group.Lines.Select(line =>
            {
                var source = lineLookup[line.SessionLineId];
                return new OverweightSplitPreviewLineItem(
                    line.SessionLineId,
                    line.SequenceNo,
                    source.ErpVehicleRegistrationId,
                    source.CustomerName,
                    source.ProductName,
                    group.SplitSequence,
                    line.AllocatedWeight,
                    line.AllocatedBagCount);
            }))
            .ToList();

        return new OverweightSplitPreviewDto(
            session.Id,
            session.Ttcp10WeightSnapshot ?? 0m,
            session.NetWeight ?? 0m,
            session.OverweightAmount,
            plan.OverweightSplitStepWeight,
            plan.SplitTicket1NetWeight,
            plan.SplitTicket2NetWeight,
            plan.RandomSplitFactor,
            plan.IsManualOverride,
            previewGroups,
            previewLines);
    }

    internal static void EnsureSessionPendingOverweight(WeighingSession session)
    {
        if (session.SessionStatus != WeighingSessionStatus.READY_TO_COMPLETE)
        {
            throw new InvalidOperationException("Luot can chua o trang thai san sang xu ly qua tai.");
        }

        if (!session.IsOverweight || session.OverweightResolutionStatus != OverweightResolutionStatus.PENDING)
        {
            throw new InvalidOperationException("Luot can hien tai khong con o trang thai cho xu ly qua tai.");
        }
    }
}

public sealed class ResolveWeighingSessionOverweightSplitUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IAppConfigRepository _configRepo;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;
    private readonly WeighingSessionOverweightService _overweightService;

    public ResolveWeighingSessionOverweightSplitUseCase(
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IAppConfigRepository configRepo,
        ITicketNumberGenerator ticketNoGen,
        IDeliveryNumberGenerator deliveryNoGen,
        ICurrentUserContext userContext,
        IClock clock,
        IUnitOfWork uow,
        WeighingSessionOverweightService overweightService)
    {
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _configRepo = configRepo;
        _ticketNoGen = ticketNoGen;
        _deliveryNoGen = deliveryNoGen;
        _userContext = userContext;
        _clock = clock;
        _uow = uow;
        _overweightService = overweightService;
    }

    public Task ExecuteAsync(Guid sessionId, CancellationToken ct)
        => ExecuteAsync(new ResolveWeighingSessionOverweightSplitRequest(sessionId), ct);

    public async Task ExecuteAsync(ResolveWeighingSessionOverweightSplitRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Khong tim thay luot can.");
        PreviewWeighingSessionOverweightSplitUseCase.EnsureSessionPendingOverweight(session);

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(request.SessionId, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(request.SessionId, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(request.SessionId, ct);
        var masterTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted)
            ?? throw new InvalidOperationException("Khong tim thay phieu can tong cua luot can.");
        var normalDeliveryByLineId = deliveryTickets
            .Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal && !x.IsDeleted && x.WeighingSessionLineId.HasValue)
            .GroupBy(x => x.WeighingSessionLineId!.Value)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).First());

        var splitStepWeight = await ResolveSplitStepWeightAsync(_configRepo, ct);
        var plan = _overweightService.BuildSplitPlan(
            session,
            lines,
            splitStepWeight,
            request.FirstSplitNetWeight,
            request.IsManualOverride);
        var now = _clock.NowLocal;
        var nextTicketNumbers = new Queue<string>(await AllocateSequentialNumbersAsync(
            () => _ticketNoGen.GenerateAsync(ct),
            plan.Groups.Count,
            ct));
        var nextDeliveryNumbers = new Queue<string>(await AllocateSequentialNumbersAsync(
            () => _deliveryNoGen.GenerateAsync(ct),
            plan.Groups.Sum(x => x.Lines.Count),
            ct));

        SoftDeleteSplitDocuments(weighTickets, deliveryTickets, now);

        var splitWeighTickets = new List<WeighTicket>();
        var splitDeliveryTickets = new List<DeliveryTicket>();
        var currentStartWeight = masterTicket.Weight1.GetValueOrDefault();

        foreach (var group in plan.Groups.OrderBy(x => x.SplitSequence))
        {
            var splitTicket = BuildSplitWeighTicket(
                session,
                masterTicket,
                group,
                currentStartWeight,
                nextTicketNumbers.Dequeue(),
                now);
            splitWeighTickets.Add(splitTicket);
            currentStartWeight = splitTicket.Weight2.GetValueOrDefault();

            foreach (var part in group.Lines.OrderBy(x => x.SequenceNo))
            {
                var line = lines.First(x => x.Id == part.SessionLineId);
                normalDeliveryByLineId.TryGetValue(part.SessionLineId, out var sourceDeliveryTicket);
                splitDeliveryTickets.Add(new DeliveryTicket
                {
                    Id = Guid.NewGuid(),
                    VehicleRegistrationId = line.VehicleRegistrationId,
                    WeighingSessionId = session.Id,
                    WeighingSessionLineId = line.Id,
                    DeliveryNo = nextDeliveryNumbers.Dequeue(),
                    ErpVehicleRegistrationId = sourceDeliveryTicket?.ErpVehicleRegistrationId ?? string.Empty,
                    CustomerCode = sourceDeliveryTicket?.CustomerCode ?? line.CustomerCode,
                    ProductCode = sourceDeliveryTicket?.ProductCode ?? line.ProductCode,
                    Notes = sourceDeliveryTicket?.Notes,
                    AllocatedWeight = part.AllocatedWeight,
                    AllocatedBagCount = part.AllocatedBagCount,
                    IsOverWeight = true,
                    RecordRole = DeliveryTicketRecordRoles.SplitDerived,
                    SplitGroupId = group.GroupId,
                    SplitSequence = group.SplitSequence,
                    SourceDeliveryTicketId = sourceDeliveryTicket?.Id,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = now,
                    CreatedBy = _userContext.Username,
                    UpdatedAt = now,
                    UpdatedBy = _userContext.Username
                });
            }
        }

        session.OverweightResolutionStatus = OverweightResolutionStatus.SPLIT_CONFIRMED;
        session.OverweightResolvedAt = now;
        session.OverweightResolvedBy = _userContext.Username;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var weighTicket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived))
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived))
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            foreach (var splitTicket in splitWeighTickets)
            {
                await _weighRepo.AddAsync(splitTicket, innerCt);
            }

            foreach (var splitTicket in splitDeliveryTickets)
            {
                await _deliveryRepo.AddAsync(splitTicket, innerCt);
            }

            await _sessionRepo.UpdateAsync(session, innerCt);
        }, ct);
    }

    internal static async Task<decimal> ResolveSplitStepWeightAsync(IAppConfigRepository configRepo, CancellationToken ct)
    {
        var raw = await configRepo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return AppConfigDefaults.DefaultOverweightSplitStepWeight;
        }

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Cau hinh OverweightSplitStepWeight khong hop le.");
        }

        return value;
    }

    private static async Task<IReadOnlyList<string>> AllocateSequentialNumbersAsync(
        Func<Task<string>> firstNumberFactory,
        int count,
        CancellationToken ct)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        ct.ThrowIfCancellationRequested();
        var firstNumber = await firstNumberFactory();
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

    private WeighTicket BuildSplitWeighTicket(
        WeighingSession session,
        WeighTicket masterTicket,
        OverweightSplitGroupPlan group,
        decimal startWeight,
        string ticketNo,
        DateTime now)
    {
        decimal weight1;
        decimal weight2;

        if (session.TransactionType == TransactionType.OUTBOUND)
        {
            weight1 = startWeight;
            weight2 = startWeight + group.GroupWeight;
        }
        else
        {
            weight1 = startWeight;
            weight2 = startWeight - group.GroupWeight;
        }

        return new WeighTicket
        {
            Id = Guid.NewGuid(),
            VehicleRegistrationId = masterTicket.VehicleRegistrationId,
            WeighingSessionId = session.Id,
            TicketNo = ticketNo,
            ErpVehicleRegistrationId = masterTicket.ErpVehicleRegistrationId,
            VehiclePlate = masterTicket.VehiclePlate,
            MoocNumber = masterTicket.MoocNumber,
            DriverName = masterTicket.DriverName,
            CustomerCode = masterTicket.CustomerCode,
            CustomerName = masterTicket.CustomerName,
            ProductCode = masterTicket.ProductCode,
            ProductName = masterTicket.ProductName,
            PlannedWeight = group.GroupWeight,
            BagCount = group.Lines.Sum(x => x.AllocatedBagCount ?? 0),
            Notes = string.IsNullOrWhiteSpace(masterTicket.Notes)
                ? $"SPLIT-{group.SplitSequence}"
                : $"{masterTicket.Notes} | SPLIT-{group.SplitSequence}",
            TransactionType = masterTicket.TransactionType,
            TransportMethod = masterTicket.TransportMethod,
            Status = TicketStatus.TICKET_COMPLETED,
            IdempotencyKey = Guid.NewGuid(),
            SyncStatus = SyncStatus.SYNC_QUEUED,
            Weight1 = weight1,
            Weight1User = masterTicket.Weight1User,
            Weight1Time = masterTicket.Weight1Time,
            Weight1UpdatedAt = masterTicket.Weight1UpdatedAt,
            Weight1Mode = masterTicket.Weight1Mode,
            Weight1IsStable = masterTicket.Weight1IsStable,
            Weight2 = weight2,
            Weight2User = masterTicket.Weight2User,
            Weight2Time = masterTicket.Weight2Time,
            Weight2UpdatedAt = now,
            Weight2Mode = masterTicket.Weight2Mode,
            Weight2IsStable = masterTicket.Weight2IsStable,
            NetWeight = group.GroupWeight,
            Ttcp10WeightSnapshot = session.Ttcp10WeightSnapshot,
            VehicleRegistrationNoSnapshot = masterTicket.VehicleRegistrationNoSnapshot,
            VehicleRegistrationExpirySnapshot = masterTicket.VehicleRegistrationExpirySnapshot,
            MoocRegistrationNoSnapshot = masterTicket.MoocRegistrationNoSnapshot,
            MoocRegistrationExpirySnapshot = masterTicket.MoocRegistrationExpirySnapshot,
            IsOverWeight = true,
            IsPrimaryDisplay = false,
            SplitGroupId = group.GroupId,
            SplitSequence = group.SplitSequence,
            SourceTicketId = masterTicket.Id,
            RecordRole = WeighTicketRecordRoles.SplitDerived,
            CreatedAt = now,
            CreatedBy = _userContext.Username,
            UpdatedAt = now,
            UpdatedBy = _userContext.Username
        };
    }

    private void SoftDeleteSplitDocuments(
        IReadOnlyList<WeighTicket> weighTickets,
        IReadOnlyList<DeliveryTicket> deliveryTickets,
        DateTime now)
    {
        foreach (var weighTicket in weighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && !x.IsDeleted))
        {
            weighTicket.IsDeleted = true;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = _userContext.Username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && !x.IsDeleted))
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = _userContext.Username;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }
    }
}

public sealed class ResolveWeighingSessionOverweightNoSplitUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public ResolveWeighingSessionOverweightNoSplitUseCase(
        IWeighingSessionRepository sessionRepo,
        ICurrentUserContext userContext,
        IClock clock,
        IUnitOfWork uow)
    {
        _sessionRepo = sessionRepo;
        _userContext = userContext;
        _clock = clock;
        _uow = uow;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Khong tim thay luot can.");
        PreviewWeighingSessionOverweightSplitUseCase.EnsureSessionPendingOverweight(session);

        var now = _clock.NowLocal;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NO_SPLIT_CONFIRMED;
        session.OverweightResolvedAt = now;
        session.OverweightResolvedBy = _userContext.Username;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateAsync(session, innerCt),
            ct);
    }
}
