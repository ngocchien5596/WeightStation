using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

public sealed class ConfigurableWeightFrameParser : IWeightFrameParser
{
    private IReadOnlyList<IWeightFrameParser> _candidateParsers;
    private string _parserType;
    private string _frameEndChar;
    private int? _weightSubstringStart;
    private int? _weightSubstringLength;

    public ConfigurableWeightFrameParser(
        string parserType = ScaleConnectionSettings.ParserTypeAuto,
        string frameEndChar = "ETX",
        int? weightSubstringStart = 0,
        int? weightSubstringLength = 7)
    {
        _parserType = ScaleConnectionSettings.NormalizeParserType(parserType);
        _frameEndChar = frameEndChar;
        _weightSubstringStart = weightSubstringStart;
        _weightSubstringLength = weightSubstringLength;
        _candidateParsers = CreateCandidateParsers();
    }

    public decimal? TryParse(string rawFrame, out bool isStable)
    {
        foreach (var parser in _candidateParsers)
        {
            var parsed = parser.TryParse(rawFrame, out isStable);
            if (parsed.HasValue)
            {
                return parsed;
            }
        }

        isStable = false;
        return null;
    }

    public void ApplyConfiguration(string? parserType, string? frameEndChar, int? weightSubstringStart, int? weightSubstringLength)
    {
        var normalizedParserType = ScaleConnectionSettings.NormalizeParserType(parserType);
        var normalizedFrameEnd = string.IsNullOrWhiteSpace(frameEndChar) ? "CR" : frameEndChar.Trim();

        _parserType = normalizedParserType;
        _frameEndChar = normalizedFrameEnd;
        _weightSubstringStart = weightSubstringStart;
        _weightSubstringLength = weightSubstringLength;

        _candidateParsers = CreateCandidateParsers();
    }

    private IReadOnlyList<IWeightFrameParser> CreateCandidateParsers()
    {
        var configuredTerminator = ScaleConnectionSettings.ResolveFrameTerminator(
            _frameEndChar,
            fallback: _parserType == ScaleConnectionSettings.ParserTypeYaohua ? '\r' : (char)0x03);

        var configuredLegacyParser = new LegacyWeightFrameParser(
            configuredTerminator,
            _weightSubstringStart,
            _weightSubstringLength);

        var configuredYaohuaParser = new YaohuaWeightFrameParser(configuredTerminator)
        {
            WeightSubstringStart = _weightSubstringStart,
            WeightSubstringLength = _weightSubstringLength
        };

        var legacyEtxFallback = new LegacyWeightFrameParser(
            (char)0x03,
            _weightSubstringStart,
            _weightSubstringLength);

        var yaohuaCrFallback = new YaohuaWeightFrameParser('\r')
        {
            WeightSubstringStart = null,
            WeightSubstringLength = null
        };

        var candidateParsers = new List<IWeightFrameParser>();

        if (_parserType == ScaleConnectionSettings.ParserTypeYaohua)
        {
            candidateParsers.Add(configuredYaohuaParser);
            if (configuredTerminator != '\r' || _weightSubstringStart.HasValue || _weightSubstringLength.HasValue)
            {
                candidateParsers.Add(yaohuaCrFallback);
            }
            candidateParsers.Add(legacyEtxFallback);
            return candidateParsers;
        }

        candidateParsers.Add(configuredLegacyParser);
        if (configuredTerminator != (char)0x03)
        {
            candidateParsers.Add(legacyEtxFallback);
        }
        candidateParsers.Add(yaohuaCrFallback);

        return _parserType switch
        {
            ScaleConnectionSettings.ParserTypeAuto =>
                candidateParsers,
            _ =>
                candidateParsers
        };
    }
}
