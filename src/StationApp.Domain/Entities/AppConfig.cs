namespace StationApp.Domain.Entities;

public class AppConfig
{
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}
