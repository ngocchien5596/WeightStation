using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class WeighingSessionLine
{
    public Guid Id { get; set; }
    public Guid WeighingSessionId { get; set; }
    public Guid CutOrderId { get; set; }
    public int SequenceNo { get; set; }

    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? DistributorCode { get; set; }
    public string? DistributorName { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal? PlannedWeight { get; set; }
    public int? PlannedBagCount { get; set; }

    public decimal? ActualAllocatedWeight { get; set; }
    public int? ActualAllocatedBagCount { get; set; }
    public WeighingSessionLineStatus LineStatus { get; set; } = WeighingSessionLineStatus.PENDING;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public bool HasPrintedDeliveryTicket { get; set; }
    public Guid? DeliveryTicketId { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.SYNC_QUEUED;
    public DateTime? LastSyncAttemptAt { get; set; }
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

