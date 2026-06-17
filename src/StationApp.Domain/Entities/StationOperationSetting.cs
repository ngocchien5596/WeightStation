namespace StationApp.Domain.Entities;

public class StationOperationSetting
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
