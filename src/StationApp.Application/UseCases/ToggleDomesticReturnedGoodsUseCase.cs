using System.Text.Json;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class ToggleDomesticReturnedGoodsUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public ToggleDomesticReturnedGoodsUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository cutOrderRepo,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _cutOrderRepo = cutOrderRepo;
        _auditLogRepo = auditLogRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, bool isReturnedGoods, CancellationToken ct)
    {
        if (!StationAuthorization.IsManager(_userContext.RoleCode) && !StationAuthorization.IsAdmin(_userContext.RoleCode))
        {
            throw new UnauthorizedAccessException("Chỉ Quản lý hoặc Quản trị hệ thống được đánh dấu Hoàn hàng nội địa.");
        }

        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân cần cập nhật.");

        if (!string.Equals(session.StationCode, _userContext.StationCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Không được cập nhật lượt cân khác trạm đang thao tác.");
        }

        if (session.IsDeleted || session.IsCancelled || session.IsNoLoad)
        {
            throw new InvalidOperationException("Lượt cân không còn hợp lệ để đánh dấu Hoàn hàng.");
        }

        if (session.TransactionType != TransactionType.OUTBOUND || session.SessionStatus != WeighingSessionStatus.COMPLETED)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ đánh dấu Hoàn hàng cho lượt cân xuất nội địa đã hoàn thành.");
        }

        var cutOrders = (await _cutOrderRepo.GetByWeighingSessionIdAsync(sessionId, ct))
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .ToList();

        if (cutOrders.Count == 0 || cutOrders.Any(x => x.IsExportScale || x.TransactionType != TransactionType.OUTBOUND))
        {
            throw new InvalidOperationException("Chỉ hỗ trợ đánh dấu Hoàn hàng cho xe xuất nội địa.");
        }

        if (session.IsReturnedBrokenTrip == isReturnedGoods)
        {
            return;
        }

        var now = _clock.NowLocal;
        var oldValue = session.IsReturnedBrokenTrip;
        session.IsReturnedBrokenTrip = isReturnedGoods;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        var normalizedAuditDetail = new AuditLogDetailBuilder()
            .WithSubject(nameof(WeighingSession.SessionNo), session.SessionNo)
            .WithSubject(nameof(WeighingSession.VehiclePlate), session.VehiclePlate)
            .AddChange(nameof(WeighingSession.IsReturnedBrokenTrip), oldValue, isReturnedGoods)
            .WithSummary(nameof(WeighingSession.NetWeight), session.NetWeight)
            .WithSummary("CutOrderCodes", cutOrders.Select(x => x.ErpCutOrderId).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray())
            .AddNote(isReturnedGoods
                ? "Đánh dấu xe hàng hoàn nội địa, không tính vào báo cáo xuất hàng nội địa."
                : "Bỏ đánh dấu xe hàng hoàn nội địa, tính lại vào báo cáo xuất hàng nội địa.")
            .Build();

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TOGGLE_DOMESTIC_RETURNED_GOODS",
            EntityType = nameof(WeighingSession),
            EntityId = session.Id,
            DetailJson = JsonSerializer.Serialize(normalizedAuditDetail, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = now,
            StationCode = _userContext.StationCode
        };

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            await _auditLogRepo.AddAsync(auditLog, innerCt);
        }, ct);
    }
}
