using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using StationApp.Device.Implementations;
using StationApp.Domain.Constants;

namespace StationApp.UI.Services;

public sealed class LocalScaleDeviceSettingsStore
{
    private const string SectionName = "ScaleDevice";
    private readonly IConfiguration _configuration;

    public LocalScaleDeviceSettingsStore(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SerialScaleDeviceConfiguration GetConfiguration()
    {
        var section = _configuration.GetSection(SectionName);

        return ScaleDeviceConfigurationResolver.BuildConfiguration(
            section["ComPort"],
            section["Baudrate"],
            section["Parity"],
            section["DataBits"],
            section["StopBits"],
            section["ParserType"],
            section["FrameEndChar"],
            section["StableCycles"],
            section["WeightSubstringStart"],
            section["WeightSubstringLength"]);
    }

    public async Task SaveAsync(SerialScaleDeviceConfiguration configuration, CancellationToken ct)
    {
        var path = ResolveAppSettingsPath();
        JsonObject root;

        if (File.Exists(path))
        {
            var content = await File.ReadAllTextAsync(path, ct);
            root = string.IsNullOrWhiteSpace(content)
                ? new JsonObject()
                : JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root[SectionName] = new JsonObject
        {
            ["ComPort"] = configuration.ComPort ?? AppConfigDefaults.DefaultDeviceComPort,
            ["Baudrate"] = configuration.BaudRate.ToString(),
            ["Parity"] = configuration.Parity,
            ["DataBits"] = configuration.DataBits,
            ["StopBits"] = configuration.StopBits,
            ["ParserType"] = configuration.ParserType,
            ["FrameEndChar"] = configuration.FrameEndChar,
            ["StableCycles"] = (configuration.StableCycles ?? ScaleConnectionSettings.ResolveStableCycles(AppConfigDefaults.DefaultDeviceStableCycles)).ToString(),
            ["WeightSubstringStart"] = (configuration.WeightSubstringStart ?? ScaleConnectionSettings.ResolveOptionalInt(AppConfigDefaults.DefaultWeightSubstringStart)).ToString(),
            ["WeightSubstringLength"] = (configuration.WeightSubstringLength ?? ScaleConnectionSettings.ResolveOptionalInt(AppConfigDefaults.DefaultWeightSubstringLength)).ToString()
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(path, root.ToJsonString(options), ct);
        if (_configuration is IConfigurationRoot rootConfiguration)
        {
            rootConfiguration.Reload();
        }
    }

    private static string ResolveAppSettingsPath()
        => Path.Combine(AppContext.BaseDirectory, "appsettings.json");
}
