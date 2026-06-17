using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class SyncOutbox
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public OutboxStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
