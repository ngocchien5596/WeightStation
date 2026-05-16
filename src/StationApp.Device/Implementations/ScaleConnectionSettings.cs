using System.IO.Ports;
using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

public static class ScaleConnectionSettings
{
    public const string ParserTypeAuto = "AUTO";
    public const string ParserTypeDefault = "DEFAULT";
    public const string ParserTypeYaohua = "YAOHUA";

    public static string NormalizeParserType(string? parserType)
    {
        if (string.Equals(parserType?.Trim(), ParserTypeAuto, StringComparison.OrdinalIgnoreCase))
        {
            return ParserTypeAuto;
        }

        return string.Equals(parserType?.Trim(), ParserTypeYaohua, StringComparison.OrdinalIgnoreCase)
            ? ParserTypeYaohua
            : ParserTypeDefault;
    }

    public static int ResolveBaudRate(string? raw, int fallback = 9600)
    {
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }

    public static Parity ResolveParity(string? raw, Parity fallback = Parity.None)
    {
        return Enum.TryParse<Parity>(raw, true, out var value) ? value : fallback;
    }

    public static int ResolveDataBits(string? raw, int fallback = 8)
    {
        return int.TryParse(raw, out var value) && value is >= 5 and <= 8 ? value : fallback;
    }

    public static StopBits ResolveStopBits(string? raw, StopBits fallback = StopBits.One)
    {
        return Enum.TryParse<StopBits>(raw, true, out var value) && value != StopBits.None ? value : fallback;
    }

    public static int ResolveStableCycles(string? raw, int fallback = 3)
    {
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }

    public static int? ResolveOptionalInt(string? raw)
    {
        return int.TryParse(raw, out var value) && value >= 0 ? value : null;
    }

    public static char ResolveFrameTerminator(string? raw, char fallback = '\r')
    {
        var normalized = raw?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "CR" or "\\R" or "13" => '\r',
            "LF" or "\\N" or "10" => '\n',
            "ETX" or "0X03" or "3" => (char)0x03,
            _ when !string.IsNullOrEmpty(raw) && raw!.Length == 1 => raw[0],
            _ => fallback
        };
    }

    public static IWeightFrameParser CreateParser(
        string? parserType,
        string? frameEndChar,
        int? weightSubstringStart,
        int? weightSubstringLength)
    {
        var normalized = NormalizeParserType(parserType);
        if (normalized == ParserTypeAuto)
        {
            return new ConfigurableWeightFrameParser(
                parserType: ParserTypeAuto,
                frameEndChar: frameEndChar ?? "ETX",
                weightSubstringStart: weightSubstringStart,
                weightSubstringLength: weightSubstringLength);
        }

        if (normalized == ParserTypeYaohua)
        {
            return new YaohuaWeightFrameParser(ResolveFrameTerminator(frameEndChar))
            {
                WeightSubstringStart = weightSubstringStart,
                WeightSubstringLength = weightSubstringLength
            };
        }

        return new LegacyWeightFrameParser(
            ResolveFrameTerminator(frameEndChar, fallback: (char)0x03),
            weightSubstringStart,
            weightSubstringLength);
    }
}
