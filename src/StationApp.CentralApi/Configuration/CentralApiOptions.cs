namespace StationApp.CentralApi.Configuration;

public sealed class CentralApiOptions
{
    public const string SectionName = "CentralApi";

    public string ApiKey { get; set; } = "changeme";
}
