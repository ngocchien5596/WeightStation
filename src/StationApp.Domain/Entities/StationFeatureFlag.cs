namespace StationApp.Domain.Entities;

public class StationFeatureFlag
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string FeatureKey { get; set; } = string.Empty;
    public string FeatureValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
