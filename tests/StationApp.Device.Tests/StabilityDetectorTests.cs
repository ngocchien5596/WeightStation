using Xunit;
using StationApp.Device.Implementations;

namespace StationApp.Device.Tests;

public class StabilityDetectorTests
{
    [Fact]
    public void Returns_Unstable_When_Insufficient_Readings()
    {
        var detector = new StabilityDetector(threshold: 5m, requiredCycles: 3);
        Assert.False(detector.AddReading(10000m));
        Assert.False(detector.AddReading(10001m));
    }

    [Fact]
    public void Returns_Stable_When_Within_Threshold()
    {
        var detector = new StabilityDetector(threshold: 5m, requiredCycles: 3);
        detector.AddReading(10000m);
        detector.AddReading(10002m);
        Assert.True(detector.AddReading(10003m));
    }

    [Fact]
    public void Returns_Unstable_When_Outside_Threshold()
    {
        var detector = new StabilityDetector(threshold: 5m, requiredCycles: 3);
        detector.AddReading(10000m);
        detector.AddReading(10003m);
        Assert.False(detector.AddReading(10010m));
    }

    [Fact]
    public void Reset_Clears_State()
    {
        var detector = new StabilityDetector(threshold: 5m, requiredCycles: 3);
        detector.AddReading(10000m);
        detector.AddReading(10001m);
        detector.AddReading(10002m);
        detector.Reset();
        Assert.False(detector.AddReading(10000m));
    }
}
