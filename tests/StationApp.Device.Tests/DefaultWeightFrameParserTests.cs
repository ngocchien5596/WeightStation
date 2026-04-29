using Xunit;
using StationApp.Device.Implementations;

namespace StationApp.Device.Tests;

public class DefaultWeightFrameParserTests
{
    [Fact]
    public void Parses_Valid_Frame()
    {
        var parser = new DefaultWeightFrameParser();
        var result = parser.TryParse("ST,GS,  12345.678 kg", out var isStable);
        Assert.NotNull(result);
        Assert.True(isStable);
        Assert.Equal(12345.678m, result);
    }

    [Fact]
    public void Returns_Null_For_Garbage()
    {
        var parser = new DefaultWeightFrameParser();
        var result = parser.TryParse("xyz", out _);
        Assert.Null(result);
    }

    [Fact]
    public void Detects_Unstable_Frame()
    {
        var parser = new DefaultWeightFrameParser();
        var result = parser.TryParse("US,NT,  25000.000 kg", out var isStable);
        Assert.NotNull(result);
        Assert.False(isStable);
    }
}
