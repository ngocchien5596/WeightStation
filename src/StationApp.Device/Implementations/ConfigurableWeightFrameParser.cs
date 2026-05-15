using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

public sealed class ConfigurableWeightFrameParser : IWeightFrameParser
{
    private IWeightFrameParser _innerParser;
    private string _parserType;
    private string _frameEndChar;
    private int? _weightSubstringStart;
    private int? _weightSubstringLength;

    public ConfigurableWeightFrameParser(
        string parserType = ScaleConnectionSettings.ParserTypeYaohua,
        string frameEndChar = "CR",
        int? weightSubstringStart = null,
        int? weightSubstringLength = null)
    {
        _parserType = ScaleConnectionSettings.NormalizeParserType(parserType);
        _frameEndChar = frameEndChar;
        _weightSubstringStart = weightSubstringStart;
        _weightSubstringLength = weightSubstringLength;
        _innerParser = CreateInnerParser();
    }

    public decimal? TryParse(string rawFrame, out bool isStable)
    {
        return _innerParser.TryParse(rawFrame, out isStable);
    }

    public void ApplyConfiguration(string? parserType, string? frameEndChar, int? weightSubstringStart, int? weightSubstringLength)
    {
        var normalizedParserType = ScaleConnectionSettings.NormalizeParserType(parserType);
        var normalizedFrameEnd = string.IsNullOrWhiteSpace(frameEndChar) ? "CR" : frameEndChar.Trim();

        var requiresParserRebuild =
            !string.Equals(_parserType, normalizedParserType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_frameEndChar, normalizedFrameEnd, StringComparison.OrdinalIgnoreCase);

        _parserType = normalizedParserType;
        _frameEndChar = normalizedFrameEnd;
        _weightSubstringStart = weightSubstringStart;
        _weightSubstringLength = weightSubstringLength;

        if (requiresParserRebuild)
        {
            _innerParser = CreateInnerParser();
        }
        else if (_innerParser is YaohuaWeightFrameParser yaohuaParser)
        {
            yaohuaParser.WeightSubstringStart = _weightSubstringStart;
            yaohuaParser.WeightSubstringLength = _weightSubstringLength;
            yaohuaParser.ResetBuffer();
        }
    }

    private IWeightFrameParser CreateInnerParser()
    {
        return ScaleConnectionSettings.CreateParser(
            _parserType,
            _frameEndChar,
            _weightSubstringStart,
            _weightSubstringLength);
    }
}
