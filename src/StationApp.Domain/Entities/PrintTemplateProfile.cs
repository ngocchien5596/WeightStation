namespace StationApp.Domain.Entities;

public class PrintTemplateProfile
{
    public Guid Id { get; set; }
    public string TemplateKind { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public double OffsetXmm { get; set; }
    public double OffsetYmm { get; set; }
    public int TemplateVersion { get; set; }
    public string LayoutJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
