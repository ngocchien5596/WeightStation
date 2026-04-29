using System;
using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class VehicleRegistration
{
    public Guid Id { get; set; }
    public string? ErpVehicleRegistrationId { get; set; }
    public RegistrationSource RegistrationSource { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public TransactionType TransactionType { get; set; }
    public TransportMethod? TransportMethod { get; set; }

    public string VehiclePlate { get; set; } = string.Empty;
    public string? MoocNumber { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverIdNo { get; set; }

    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }

    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? CutOrderCode { get; set; }
    public string? OrderCode { get; set; }
    public string? LotNo { get; set; }
    public string? RepresentativeName { get; set; }
    public string? ConsumptionPlace { get; set; }
    public string? LoadingPlace { get; set; }
    public string? SealNo { get; set; }

    public decimal? PlannedWeight { get; set; }
    public int? BagCount { get; set; }
    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }
    public bool HasOverweightCase { get; set; }
    public ProcessingStage ProcessingStage { get; set; } = ProcessingStage.IN_YARD;

    public Guid? CurrentPrimaryWeighTicketId { get; set; }
    public Guid? CurrentPrimaryDeliveryTicketId { get; set; }

    public SyncStatus SyncStatus { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string? AppVersion { get; set; }
    
    public bool IsInboundProcessed { get; set; }
    public DateTime? InboundProcessedAt { get; set; }
    public string? InboundErrorCode { get; set; }
    public string? InboundErrorMessage { get; set; }
    
    public DateTime? LastInboundAttemptAt { get; set; }
    public DateTime? LastSyncAttemptAt { get; set; }
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
