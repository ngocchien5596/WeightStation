using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> SearchEditLogsAsync(string? vehiclePlate, string? sessionNo, DateTime fromDate, DateTime toDate, string? stationCode, CancellationToken ct);
}
