using System.Text.Json;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class ToggleCrusherReturnedBrokenTripUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public ToggleCrusherReturnedBrokenTripUseCase(
        IWeighingSessionRepository sessionRepo,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _auditLogRepo = auditLogRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, bool isReturnedBrokenTrip, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y lÆ°á»£t cÃ¢n má» Ä‘Ã¡ cáº§n cáº­p nháº­t.");

        if (session.IsDeleted || session.IsCancelled)
        {
            throw new InvalidOperationException("LÆ°á»£t cÃ¢n khÃ´ng cÃ²n há»£p lá»‡ Ä‘á»ƒ cáº­p nháº­t tráº¡ng thÃ¡i hÃ ng hoÃ n.");
        }

        if (string.IsNullOrWhiteSpace(session.InternalVehicleNo))
        {
            throw new InvalidOperationException("Chá»‰ há»— trá»£ Ä‘Ã¡nh dáº¥u hÃ ng hoÃ n cho luá»“ng cÃ¢n má» Ä‘Ã¡.");
        }

        if (session.SessionStatus != WeighingSessionStatus.COMPLETED || (session.NetWeight ?? 0m) <= 0m)
        {
            throw new InvalidOperationException("Chá»‰ Ä‘Æ°á»£c Ä‘Ã¡nh dáº¥u hÃ ng hoÃ n khi lÆ°á»£t cÃ¢n Ä‘Ã£ hoÃ n thÃ nh vÃ  cÃ³ trá»ng lÆ°á»£ng hÃ ng.");
        }

        if (session.IsReturnedBrokenTrip == isReturnedBrokenTrip)
        {
            return;
        }

        ReturnedBrokenTripPreviousTripInfo? previousTrip = null;
        ReturnedBrokenTripWeightResolution? resolution = null;
        var oldState = session.IsReturnedBrokenTrip;
        var oldNetWeight = session.NetWeight;
        var actualNetWeight = ResolveActualNetWeight(session);

        if (isReturnedBrokenTrip)
        {
            previousTrip = await _sessionRepo.GetPreviousCrusherTripForReturnedAsync(session.Id, ct);
            if (previousTrip == null)
            {
                throw new InvalidOperationException("KhÃ´ng cÃ³ dá»¯ liá»‡u chuyáº¿n xe gáº§n nháº¥t trÆ°á»›c Ä‘Ã³ cá»§a xe nÃ y. Vui lÃ²ng kiá»ƒm tra láº¡i.");
            }

            resolution = ReturnedBrokenTripWeightLimiter.Resolve(actualNetWeight, previousTrip.NetWeightKg);
        }

        var now = _clock.NowLocal;
        session.IsReturnedBrokenTrip = isReturnedBrokenTrip;
        session.NetWeight = isReturnedBrokenTrip
            ? resolution!.RecognizedWeightKg
            : actualNetWeight;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        var normalizedAuditDetail = new AuditLogDetailBuilder()
            .WithSubject("Name", session.SessionNo)
            .WithSubject(nameof(WeighingSession.VehiclePlate), session.InternalVehicleNo ?? session.VehiclePlate)
            .AddChange("IsReturnedBrokenTrip", oldState, isReturnedBrokenTrip)
            .AddChange("ReturnedWeight", actualNetWeight, session.NetWeight, "kg")
            .WithSummary(nameof(WeighingSession.Weight1), session.Weight1)
            .WithSummary("PreviousTripSessionNo", previousTrip?.SessionNo)
            .WithSummary("PreviousTripWeight", previousTrip?.NetWeightKg)
            .WithSummary("IsReturnedWeightCapped", resolution?.IsCapped ?? false)
            .AddNote(isReturnedBrokenTrip
                ? "\u0110\u00e1nh d\u1ea5u h\u00e0ng ho\u00e0n m\u1ecf \u0111\u00e1"
                : "B\u1ecf \u0111\u00e1nh d\u1ea5u h\u00e0ng ho\u00e0n m\u1ecf \u0111\u00e1")
            .Build();

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(
                normalizedAuditDetail,
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(
            async innerCt =>
            {
                await _sessionRepo.UpdateAsync(session, innerCt);
                await _auditLogRepo.AddAsync(auditLog, innerCt);
            },
            ct);
    }

    private static decimal ResolveActualNetWeight(WeighingSession session)
    {
        if (string.Equals(session.NetWeightCalculationMode, NetWeightCalculationModes.Weight1MinusStandardTare, StringComparison.OrdinalIgnoreCase)
            && session.Weight1.HasValue
            && session.StandardTareWeightSnapshot.HasValue)
        {
            return Math.Max(0m, session.Weight1.Value - session.StandardTareWeightSnapshot.Value);
        }

        if (session.Weight1.HasValue && session.Weight2.HasValue)
        {
            return Math.Abs(session.Weight2.Value - session.Weight1.Value);
        }

        return Math.Max(0m, session.NetWeight ?? 0m);
    }
}
