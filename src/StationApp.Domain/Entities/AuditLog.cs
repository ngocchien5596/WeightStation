namespace StationApp.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? StationCode { get; set; }
}
