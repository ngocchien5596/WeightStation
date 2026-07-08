using System.Text.Json;
using StationApp.Application.Interfaces;
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
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân mỏ đá cần cập nhật.");

        if (session.IsDeleted || session.IsCancelled)
        {
            throw new InvalidOperationException("Lượt cân không còn hợp lệ để cập nhật trạng thái hàng hoàn.");
        }

        if (string.IsNullOrWhiteSpace(session.InternalVehicleNo))
        {
            throw new InvalidOperationException("Chỉ hỗ trợ đánh dấu hàng hoàn cho luồng cân mỏ đá.");
        }

        if (session.SessionStatus != WeighingSessionStatus.COMPLETED || (session.NetWeight ?? 0m) <= 0m)
        {
            throw new InvalidOperationException("Chỉ được đánh dấu hàng hoàn khi lượt cân đã hoàn thành và có trọng lượng hàng.");
        }

        if (session.IsReturnedBrokenTrip == isReturnedBrokenTrip)
        {
            return;
        }

        var now = _clock.NowLocal;
        var oldState = session.IsReturnedBrokenTrip;
        session.IsReturnedBrokenTrip = isReturnedBrokenTrip;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        var auditDetail = new
        {
            SessionNo = session.SessionNo,
            VehiclePlate = session.InternalVehicleNo ?? session.VehiclePlate,
            GrossWeight = session.Weight1,
            NetWeight = session.NetWeight,
            OldIsReturnedBrokenTrip = oldState,
            NewIsReturnedBrokenTrip = isReturnedBrokenTrip,
            Note = isReturnedBrokenTrip
                ? "Đánh dấu hàng hoàn mỏ đá"
                : "Bỏ đánh dấu hàng hoàn mỏ đá"
        };

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(
                auditDetail,
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
}
