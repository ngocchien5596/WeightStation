using StationApp.Device.Implementations;
using StationApp.Domain.Constants;

namespace StationApp.UI.Services;

public sealed class ScaleDeviceConfigurationResolver
{
    private readonly LocalScaleDeviceSettingsStore _settingsStore;

    public ScaleDeviceConfigurationResolver(LocalScaleDeviceSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public Task<SerialScaleDeviceConfiguration?> GetSavedConfigurationAsync(CancellationToken ct)
        => Task.FromResult<SerialScaleDeviceConfiguration?>(_settingsStore.GetConfiguration());

    public static SerialScaleDeviceConfiguration BuildConfiguration(
        string? comPort,
        string? baudRateRaw,
        string? parity,
        string? dataBits,
        string? stopBits,
        string? parserType,
        string? frameEndChar,
        string? stableCyclesRaw,
        string? startRaw,
        string? lengthRaw)
    {
        var resolvedComPort = string.IsNullOrWhiteSpace(comPort)
            ? AppConfigDefaults.DefaultDeviceComPort
            : comPort.Trim();

        var resolvedBaudRate = ScaleConnectionSettings.ResolveBaudRate(
            baudRateRaw,
            fallback: int.Parse(AppConfigDefaults.DefaultDeviceBaudrate));

        var resolvedParity = string.IsNullOrWhiteSpace(parity)
            ? AppConfigDefaults.DefaultDeviceParity
            : parity.Trim();

        var resolvedDataBits = string.IsNullOrWhiteSpace(dataBits)
            ? AppConfigDefaults.DefaultDeviceDataBits
            : dataBits.Trim();

        var resolvedStopBits = string.IsNullOrWhiteSpace(stopBits)
            ? AppConfigDefaults.DefaultDeviceStopBits
            : stopBits.Trim();

        var resolvedParserType = string.IsNullOrWhiteSpace(parserType)
            ? AppConfigDefaults.DefaultDeviceParserType
            : parserType.Trim();

        var resolvedFrameEndChar = string.IsNullOrWhiteSpace(frameEndChar)
            ? AppConfigDefaults.DefaultDeviceFrameEndChar
            : frameEndChar.Trim();

        var resolvedStableCycles = ScaleConnectionSettings.ResolveStableCycles(
            stableCyclesRaw,
            fallback: int.Parse(AppConfigDefaults.DefaultDeviceStableCycles));

        var resolvedStart = ScaleConnectionSettings.ResolveOptionalInt(startRaw ?? AppConfigDefaults.DefaultWeightSubstringStart);
        var resolvedLength = ScaleConnectionSettings.ResolveOptionalInt(lengthRaw ?? AppConfigDefaults.DefaultWeightSubstringLength);

        return new SerialScaleDeviceConfiguration(
            resolvedComPort,
            resolvedBaudRate,
            resolvedParity,
            resolvedDataBits,
            resolvedStopBits,
            resolvedParserType,
            resolvedFrameEndChar,
            resolvedStableCycles,
            resolvedStart,
            resolvedLength);
    }
}
