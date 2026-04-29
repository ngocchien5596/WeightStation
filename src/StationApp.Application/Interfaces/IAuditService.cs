namespace StationApp.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, Guid entityId, object? detail, CancellationToken ct);
}
