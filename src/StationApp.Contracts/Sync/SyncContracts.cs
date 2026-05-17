namespace StationApp.Contracts.Sync;

public sealed class SyncWeighTicketRequest
{
    public Guid Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public sealed class SyncWeighTicketResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
