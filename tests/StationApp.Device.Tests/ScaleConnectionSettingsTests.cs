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
    public void CreateParser_Default_ParsesLegacyNumericFrameWithEtx()
    {
        var parser = ScaleConnectionSettings.CreateParser("DEFAULT", "ETX", 0, 7);

        var result = parser.TryParse($"0014000{(char)0x03}", out var isStable);

        Assert.Equal(14000m, result);
        Assert.False(isStable);
    }

    [Fact]
    public void CreateParser_Auto_ParsesLegacyNumericFrameWithEtx()
    {
        var parser = ScaleConnectionSettings.CreateParser("AUTO", "ETX", 0, 7);

        var result = parser.TryParse($"0025350{(char)0x03}", out var isStable);

        Assert.Equal(25350m, result);
        Assert.False(isStable);
    }

    [Fact]
    public void CreateParser_Auto_ParsesLegacyDisplayedFrameWithoutRawEtxCharacter()
    {
        var parser = ScaleConnectionSettings.CreateParser("AUTO", "ETX", 0, 7);

        var result = parser.TryParse("+018670013", out var isStable);

        Assert.Equal(18670m, result);
        Assert.False(isStable);
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
