namespace StationApp.CentralApi.Persistence;

public sealed class SyncIngestionLog
{
    public Guid Id { get; set; }
    public string? StationCode { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid SourceRecordId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
