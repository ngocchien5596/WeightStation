using Xunit;
using StationApp.Device.Implementations;
using StationApp.Device.Models;

namespace StationApp.Device.Tests;

public class SimulatorScaleDeviceTests
{
    [Fact]
    public async Task Simulator_Emits_Readings()
    {
        var device = new SimulatorScaleDevice();
        var readings = new List<ScaleReading>();
        device.WeightReceived += (_, r) => readings.Add(r);

        await device.ConnectAsync(CancellationToken.None);
        await device.StartAsync(CancellationToken.None);

        await Task.Delay(2000);

        await device.StopAsync(CancellationToken.None);

        Assert.True(readings.Count >= 2, $"Expected at least 2 readings, got {readings.Count}");
        Assert.All(readings, r => Assert.True(r.Weight >= 0));
    }
}
