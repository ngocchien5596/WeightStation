using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class UpdateWeighingSessionMoocNumberUseCase
{
    private readonly IWeighingSessionRepository _weighingSessionRepository;
    private readonly ICutOrderRepository _cutOrderRepository;
    private readonly IWeighTicketRepository _weighTicketRepository;
    private readonly IErpCutOrderWriteBackService _erpCutOrderWriteBackService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IClock _clock;
    private readonly IAuditService _auditService;

    public UpdateWeighingSessionMoocNumberUseCase(
        IWeighingSessionRepository weighingSessionRepository,
        ICutOrderRepository cutOrderRepository,
        IWeighTicketRepository weighTicketRepository,
        IErpCutOrderWriteBackService erpCutOrderWriteBackService,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IClock clock,
        IAuditService auditService)
    {
        _weighingSessionRepository = weighingSessionRepository;
        _cutOrderRepository = cutOrderRepository;
        _weighTicketRepository = weighTicketRepository;
        _erpCutOrderWriteBackService = erpCutOrderWriteBackService;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _clock = clock;
        _auditService = auditService;
    }

    public async Task<OperationResult<string?>> ExecuteAsync(Guid weighingSessionId, string? moocNumber, CancellationToken ct)
    {
        var session = await _weighingSessionRepository.GetByIdAsync(weighingSessionId, ct);
        if (session == null)
        {
            return OperationResult<string?>.Fail("Không tìm thấy lượt cân để cập nhật số mooc.");
        }

        var registrations = await _cutOrderRepository.GetByWeighingSessionIdAsync(weighingSessionId, ct);
        if (registrations.Count == 0)
        {
            return OperationResult<string?>.Fail("Không tìm thấy cắt lệnh cho lượt cân để cập nhật số mooc.");
        }

        var weighTickets = await _weighTicketRepository.GetByWeighingSessionIdAsync(weighingSessionId, ct);
        var normalizedMoocNumber = NormalizeOptional(moocNumber);
        var oldMoocNumber = session.MoocNumber;
        var now = _clock.NowLocal;
        var erpRegistrations = registrations
            .Where(ShouldWriteBackToErp)
            .ToList();

        foreach (var registration in erpRegistrations)
        {
            try
            {
                var result = await _erpCutOrderWriteBackService.UpdateMoocNoAsync(
                    new ErpCutOrderMoocWriteBackRequest(
                        registration.ErpCutOrderId!.Trim(),
                        normalizedMoocNumber,
                        _currentUserContext.Username,
                        now),
                    ct);

                if (result.AffectedRows <= 0)
                {
                    // Dữ liệu ERP cũ trước go-live có thể không còn bản ghi tương ứng để ghi ngược.
                    // Vẫn lưu thay đổi local và audit, không chặn thao tác của trạm cân.
                }
            }
            catch (Exception ex)
            {
                return OperationResult<string?>.Fail($"Không thể cập nhật ERP cho cắt lệnh {registration.ErpCutOrderId}: {ex.Message}");
            }
        }

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            session.MoocNumber = normalizedMoocNumber;
            session.UpdatedAt = now;
            session.UpdatedBy = _currentUserContext.Username;
            await _weighingSessionRepository.UpdateAsync(session, innerCt);

            foreach (var registration in registrations)
            {
                registration.MoocNumber = normalizedMoocNumber;
                registration.UpdatedAt = now;
                registration.UpdatedBy = _currentUserContext.Username;
                await _cutOrderRepository.UpdateAsync(registration, innerCt);
            }

            foreach (var weighTicket in weighTickets)
            {
                weighTicket.MoocNumber = normalizedMoocNumber;
                weighTicket.UpdatedAt = now;
                weighTicket.UpdatedBy = _currentUserContext.Username;
                await _weighTicketRepository.UpdateAsync(weighTicket, innerCt);
            }
        }, ct);

        await _auditService.LogAsync(
            "UPDATE_WEIGHING_SESSION_MOOC_NO",
            nameof(WeighingSession),
            session.Id,
            new AuditLogDetailBuilder()
                .WithSubject("Name", session.SessionNo)
                .WithSubject(nameof(WeighingSession.VehiclePlate), session.VehiclePlate)
                .AddChange("MoocNumber", oldMoocNumber, normalizedMoocNumber)
                .WithSummary("UpdatedCutOrderCount", registrations.Count)
                .WithSummary("UpdatedWeighTicketCount", weighTickets.Count)
                .WithSummary("UpdatedErpCutOrderIds", erpRegistrations.Select(x => x.ErpCutOrderId).ToArray())
                .Build(),
            ct);

        return OperationResult<string?>.Ok(normalizedMoocNumber);
    }

    private static bool ShouldWriteBackToErp(CutOrder registration)
    {
        return registration.CutOrderSource == CutOrderSource.ERP
            && !string.IsNullOrWhiteSpace(registration.ErpCutOrderId);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
