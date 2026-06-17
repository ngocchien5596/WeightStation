namespace StationApp.Domain.Entities;

public class UserStationAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public User? User { get; set; }
}
