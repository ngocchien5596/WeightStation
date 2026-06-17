using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class WeighTicket
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public Guid CutOrderId { get; set; }
    public Guid? WeighingSessionId { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string? ErpCutOrderId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public string? MoocNumber { get; set; }
    public string? DriverName { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal? PlannedWeight { get; set; }
    public int? BagCount { get; set; }
    public string? Notes { get; set; }
    public TransactionType TransactionType { get; set; }
    public TransportMethod? TransportMethod { get; set; }
    public bool IsCancelled { get; set; }
    public TicketStatus Status { get; set; }
    public Guid IdempotencyKey { get; set; }
    public SyncStatus SyncStatus { get; set; }

    // Weight 1
    public decimal? Weight1 { get; set; }
    public string? Weight1User { get; set; }
    public DateTime? Weight1Time { get; set; }
    public DateTime? Weight1UpdatedAt { get; set; }
    public WeightMode? Weight1Mode { get; set; }
    public bool? Weight1IsStable { get; set; }

    // Weight 2
    public decimal? Weight2 { get; set; }
    public string? Weight2User { get; set; }
    public DateTime? Weight2Time { get; set; }
    public DateTime? Weight2UpdatedAt { get; set; }
    public WeightMode? Weight2Mode { get; set; }
    public bool? Weight2IsStable { get; set; }

    // Calculated
    public decimal? NetWeight { get; set; }
    public string? AppVersion { get; set; }
    public string WeighingMode { get; set; } = "TWO_WEIGH";
    public string? InternalVehicleNo { get; set; }
    public decimal? StandardTareWeightSnapshot { get; set; }
    public string? StandardTareSourceSnapshot { get; set; }
    public Guid? StandardTareVehicleId { get; set; }
    public string? NetWeightCalculationMode { get; set; } = "WEIGHT2_DIFF";

    // Phase 2 Delta - Snapshots
    public decimal? Ttcp10WeightSnapshot { get; set; }
    public string? VehicleRegistrationNoSnapshot { get; set; }
    public DateTime? VehicleRegistrationExpirySnapshot { get; set; }
    public string? MoocRegistrationNoSnapshot { get; set; }
    public DateTime? MoocRegistrationExpirySnapshot { get; set; }

    // Phase 2 Delta - Flags & Logic
    public bool IsOverWeight { get; set; }
    public bool IsPrimaryDisplay { get; set; } = true;
    public bool IsPrinted { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? LastPrintedAt { get; set; }
    public string? LastPrintError { get; set; }
    public Guid? SplitGroupId { get; set; }
    public byte? SplitSequence { get; set; }
    public Guid? SourceTicketId { get; set; }
    public Guid? DeliveryTicketId { get; set; }
    public string RecordRole { get; set; } = "MASTER_SESSION";

    // Audit
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}


