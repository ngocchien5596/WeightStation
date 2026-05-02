using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class WeighingSession
{
    public Guid Id { get; set; }
    public string SessionNo { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }

    public string VehiclePlate { get; set; } = string.Empty;
    public string? MoocNumber { get; set; }
    public string? DriverName { get; set; }

    public WeighingSessionStatus SessionStatus { get; set; } = WeighingSessionStatus.PENDING_WEIGHT1;

    public decimal? Weight1 { get; set; }
    public DateTime? Weight1Time { get; set; }
    public decimal? Weight2 { get; set; }
    public DateTime? Weight2Time { get; set; }
    public decimal? NetWeight { get; set; }
    public decimal? Ttcp10WeightSnapshot { get; set; }
    public bool IsOverweight { get; set; }
    public decimal OverweightAmount { get; set; }
    public OverweightResolutionStatus OverweightResolutionStatus { get; set; } = OverweightResolutionStatus.NOT_APPLICABLE;
    public DateTime? OverweightResolvedAt { get; set; }
    public string? OverweightResolvedBy { get; set; }

    public bool IsCancelled { get; set; }
    public bool HasPrintedMasterWeighTicket { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
