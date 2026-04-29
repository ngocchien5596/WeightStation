using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class DeliveryTicket
{
    public Guid Id { get; set; }
    public Guid VehicleRegistrationId { get; set; }
    public string DeliveryNo { get; set; } = string.Empty;
    public string ErpVehicleRegistrationId { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? ProductCode { get; set; }
    public string? Notes { get; set; }
    public bool IsOverWeight { get; set; }
    public bool IsPrinted { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? LastPrintedAt { get; set; }
    public string? LastPrintError { get; set; }
    public Guid? SplitGroupId { get; set; }
    public byte? SplitSequence { get; set; }
    public Guid? SourceDeliveryTicketId { get; set; }
    public string RecordRole { get; set; } = "WORKING"; // WORKING, SOURCE
    public SyncStatus SyncStatus { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
