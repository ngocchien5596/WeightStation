using StationApp.Device.Models;

namespace StationApp.Device.Abstractions;

public interface IScaleDevice
{
    event EventHandler<ScaleReading>? WeightReceived;
    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    bool IsConnected { get; }
}
