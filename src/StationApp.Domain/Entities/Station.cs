namespace StationApp.Domain.Entities;

public class Station
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
