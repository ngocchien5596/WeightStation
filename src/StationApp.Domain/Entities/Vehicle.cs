namespace StationApp.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public string MoocNumber { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? TransportMethod { get; set; }
    public decimal? TtcpWeight { get; set; }

    public string? VehicleRegistrationNo { get; set; }
    public DateTime? VehicleRegistrationExpiryDate { get; set; }
    public string? MoocRegistrationNo { get; set; }
    public DateTime? MoocRegistrationExpiryDate { get; set; }

    public bool IsInternalVehicle { get; set; }
    public string? StandardTareSource { get; set; }
    public DateTime? StandardTareUpdatedAt { get; set; }
    public string? StandardTareUpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
