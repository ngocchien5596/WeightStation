using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

/// <summary>
/// Legacy parser for scales that stream a numeric payload terminated by a single control character
/// such as ETX (3). Optional substring slicing is preserved for compatibility with older vendor tools.
/// </summary>
public sealed class LegacyWeightFrameParser : IWeightFrameParser
{
    private string _buffer = string.Empty;
    private readonly char _frameEndChar;

    public int? WeightSubstringStart { get; set; }
    public int? WeightSubstringLength { get; set; }

    public LegacyWeightFrameParser(char frameEndChar, int? weightSubstringStart = null, int? weightSubstringLength = null)
    {
        _frameEndChar = frameEndChar;
        WeightSubstringStart = weightSubstringStart;
        WeightSubstringLength = weightSubstringLength;
    }

    public decimal? TryParse(string rawFrame, out bool isStable)
    {
        isStable = false;
        if (string.IsNullOrEmpty(rawFrame))
        {
            return null;
        }

        _buffer += rawFrame;

        var lastEnd = _buffer.LastIndexOf(_frameEndChar);
        if (lastEnd < 0)
        {
            return TryParseLooseFrame(out isStable);
        }

        var searchFrom = lastEnd - 1;
        while (searchFrom >= 0 && _buffer[searchFrom] != _frameEndChar)
        {
            searchFrom--;
        }

        var frameStart = searchFrom + 1;
        var frame = _buffer[frameStart..lastEnd].Trim();
        _buffer = _buffer[(lastEnd + 1)..];

        if (string.IsNullOrWhiteSpace(frame))
        {
            return null;
        }

        return ParseFrame(frame, out isStable);
    }

    public void ResetBuffer() => _buffer = string.Empty;

    private decimal? ParseFrame(string frame, out bool isStable)
    {
        isStable = frame.StartsWith("ST", StringComparison.OrdinalIgnoreCase);

        var slice = ApplySubstring(frame);
        if (string.IsNullOrWhiteSpace(slice))
        {
            return null;
        }

        var numericChars = new List<char>();
        var foundDigit = false;
        var foundDot = false;
        var signMultiplier = slice.Contains('-') ? -1m : 1m;

        foreach (var character in slice)
        {
            if (char.IsDigit(character))
            {
                foundDigit = true;
                numericChars.Add(character);
                continue;
            }

            if (character == '.' && foundDigit && !foundDot)
            {
                foundDot = true;
                numericChars.Add(character);
                continue;
            }

            if (foundDigit && char.IsLetter(character))
            {
                break;
            }
        }

        if (numericChars.Count == 0)
        {
            return null;
        }

        var numericText = new string(numericChars.ToArray());
        return decimal.TryParse(
            numericText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var weight)
            ? weight * signMultiplier
            : null;
    }

    private decimal? TryParseLooseFrame(out bool isStable)
    {
        var frame = _buffer.Trim();
        if (string.IsNullOrWhiteSpace(frame))
        {
            isStable = false;
            return null;
        }

        if (!HasEnoughLooseData(frame))
        {
            isStable = false;
            return null;
        }

        var parsed = ParseFrame(frame, out isStable);
        if (parsed.HasValue)
        {
            TrimLooseBuffer();
        }

        return parsed;
    }

    private bool HasEnoughLooseData(string frame)
    {
        if (WeightSubstringStart.HasValue && WeightSubstringLength.HasValue && WeightSubstringLength.Value > 0)
        {
            return frame.Length >= WeightSubstringStart.Value + WeightSubstringLength.Value;
        }

        var digitCount = frame.Count(char.IsDigit);
        return digitCount >= 5;
    }

    private void TrimLooseBuffer()
    {
        const int maxTailLength = 32;
        if (_buffer.Length <= maxTailLength)
        {
            return;
        }

        _buffer = _buffer[^maxTailLength..];
    }

    private string ApplySubstring(string frame)
    {
        if (!WeightSubstringStart.HasValue || !WeightSubstringLength.HasValue || WeightSubstringLength.Value <= 0)
        {
            return frame;
        }

        var start = WeightSubstringStart.Value;
        if (start < 0 || start >= frame.Length)
        {
            return string.Empty;
        }

        var length = Math.Min(WeightSubstringLength.Value, frame.Length - start);
        return frame.Substring(start, length);
    }
}
