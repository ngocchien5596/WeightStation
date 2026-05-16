using StationApp.Device.Implementations;
using Xunit;

namespace StationApp.Device.Tests;

public class ConfigurableWeightFrameParserTests
{
    [Fact]
    public void AutoParser_ParsesLegacyNumericEtxFrame()
    {
        var parser = new ConfigurableWeightFrameParser(
            parserType: ScaleConnectionSettings.ParserTypeAuto,
            frameEndChar: "ETX",
            weightSubstringStart: 0,
            weightSubstringLength: 7);

        var result = parser.TryParse($"0024680{(char)0x03}", out var isStable);

        Assert.Equal(24680m, result);
        Assert.False(isStable);
    }

    [Fact]
    public void YaohuaConfiguredParser_FallsBackToLegacyEtxFrame()
    {
        var parser = new ConfigurableWeightFrameParser(
            parserType: ScaleConnectionSettings.ParserTypeYaohua,
            frameEndChar: "CR",
            weightSubstringStart: null,
            weightSubstringLength: null);

        var result = parser.TryParse($"0017778{(char)0x03}", out var isStable);

        Assert.Equal(17778m, result);
        Assert.False(isStable);
    }

    [Fact]
    public void AutoParser_ParsesYaohuaVerboseCrFrame()
    {
        var parser = new ConfigurableWeightFrameParser(
            parserType: ScaleConnectionSettings.ParserTypeAuto,
            frameEndChar: "ETX",
            weightSubstringStart: 0,
            weightSubstringLength: 7);

        var result = parser.TryParse("ST,GS,+  00025350 kg\r", out var isStable);

        Assert.Equal(25350m, result);
        Assert.True(isStable);
    }

    [Fact]
    public void AutoParser_ParsesLegacyDisplayedFrameWithoutRawEtxCharacter()
    {
        var parser = new ConfigurableWeightFrameParser(
            parserType: ScaleConnectionSettings.ParserTypeAuto,
            frameEndChar: "ETX",
            weightSubstringStart: 0,
            weightSubstringLength: 7);

        var result = parser.TryParse("+018670013", out var isStable);

        Assert.Equal(18670m, result);
        Assert.False(isStable);
    }

    [Fact]
    public void AutoParser_DoesNotParseSplitLegacyChunksPrematurely()
    {
        var parser = new ConfigurableWeightFrameParser(
            parserType: ScaleConnectionSettings.ParserTypeAuto,
            frameEndChar: "ETX",
            weightSubstringStart: 0,
            weightSubstringLength: 7);

        var firstChunk = parser.TryParse("+01", out var firstStable);
        var secondChunk = parser.TryParse("6860013", out var secondStable);

        Assert.Null(firstChunk);
        Assert.False(firstStable);
        Assert.Equal(16860m, secondChunk);
        Assert.False(secondStable);
    }
}
