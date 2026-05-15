namespace StationApp.Domain.Entities;

public class AppConfig
{
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
