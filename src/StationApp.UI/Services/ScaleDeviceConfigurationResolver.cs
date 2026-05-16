using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Implementations;
using StationApp.Domain.Constants;

namespace StationApp.UI.Services;

public sealed class ScaleDeviceConfigurationResolver
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScaleDeviceConfigurationResolver(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<SerialScaleDeviceConfiguration?> GetSavedConfigurationAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var appRepo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        return BuildConfiguration(
            await appRepo.GetValueAsync(AppConfigKeys.DeviceComPort, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceBaudrate, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceParity, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceDataBits, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceStopBits, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceParserType, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceFrameEndChar, ct),
            await appRepo.GetValueAsync(AppConfigKeys.DeviceStableCycles, ct),
            await appRepo.GetValueAsync(AppConfigKeys.WeightSubstringStart, ct),
            await appRepo.GetValueAsync(AppConfigKeys.WeightSubstringLength, ct));
    }

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
