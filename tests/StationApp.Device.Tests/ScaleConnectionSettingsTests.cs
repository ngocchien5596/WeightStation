using System.IO.Ports;
using StationApp.Device.Implementations;
using Xunit;

namespace StationApp.Device.Tests;

public class ScaleConnectionSettingsTests
{
    [Fact]
    public void CreateParser_Yaohua_UsesConfiguredTerminatorAndParsesFrame()
    {
        var parser = ScaleConnectionSettings.CreateParser("YAOHUA", "CR", null, null);

        var result = parser.TryParse("ST,GS,+  00025350 kg\r", out var isStable);

        Assert.Equal(25350m, result);
        Assert.True(isStable);
    }

    [Fact]
    public void CreateParser_Default_IgnoresLegacySubstringAndParsesFullFrame()
    {
        var parser = ScaleConnectionSettings.CreateParser("DEFAULT", "ETX", 0, 7);

        var result = parser.TryParse("ST,GS,+  00014000 kg", out var isStable);

        Assert.Equal(14000m, result);
        Assert.True(isStable);
    }

    [Theory]
    [InlineData("None", Parity.None)]
    [InlineData("Even", Parity.Even)]
    [InlineData("Odd", Parity.Odd)]
    public void ResolveParity_ReturnsExpectedValue(string raw, Parity expected)
    {
        Assert.Equal(expected, ScaleConnectionSettings.ResolveParity(raw));
    }
}
