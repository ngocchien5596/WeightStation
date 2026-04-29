using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

/// <summary>
/// Parser chuyên biệt cho đầu cân Yaohua (XK3190-A12, A12E, D2+, v.v.).
/// 
/// Yaohua broadcast frame format (typical):
///   ST,GS,+  00025350 kg\r
///   ST,NT,+  00000000 kg\r
///   US,GS,+  00025340 kg\r
/// 
/// Layout (20 chars, CR-terminated):
///   [0..1]   Status: "ST" = Stable, "US" = Unstable  
///   [2]      Comma separator
///   [3..4]   Mode: "GS" = Gross, "NT" = Net
///   [5]      Comma separator
///   [6]      Sign: "+" or "-"
///   [7..8]   Spaces (padding)
///   [9..16]  Weight digits (8 chars, right-aligned, zero-padded)
///   [17]     Space
///   [18..19] Unit: "kg"
///   [20]     CR (\r) — Frame terminator
/// 
/// Some Yaohua models may use slightly different padding.
/// This parser handles both strict-format and relaxed extraction.
/// </summary>
public sealed class YaohuaWeightFrameParser : IWeightFrameParser
{
    private string _buffer = string.Empty;
    private readonly char _frameEndChar;

    public int? WeightSubstringStart { get; set; }
    public int? WeightSubstringLength { get; set; }

    public YaohuaWeightFrameParser(char frameEndChar = '\r')
    {
        _frameEndChar = frameEndChar;
    }

    /// <summary>
    /// Appends raw serial data to buffer and attempts to extract the latest complete frame.
    /// Returns null if no valid frame is found yet (incomplete data).
    /// </summary>
    public decimal? TryParse(string rawFrame, out bool isStable)
    {
        isStable = false;
        if (string.IsNullOrEmpty(rawFrame)) return null;

        _buffer += rawFrame;

        // Extract only the LAST complete frame from the buffer
        var lastEnd = _buffer.LastIndexOf(_frameEndChar);
        if (lastEnd < 0) return null; // No complete frame yet

        // Find the start of the last complete frame
        var searchFrom = lastEnd - 1;
        while (searchFrom >= 0 && _buffer[searchFrom] != _frameEndChar)
            searchFrom--;
        
        var frameStart = searchFrom + 1;
        var frame = _buffer[frameStart..lastEnd].Trim();

        // Keep only unprocessed data after the last frame end
        _buffer = _buffer[(lastEnd + 1)..];

        if (frame.Length < 10) return null;

        return ParseFrame(frame, out isStable);
    }

    private decimal? ParseFrame(string frame, out bool isStable)
    {
        isStable = false;

        try
        {
            // Detect stability from first 2 chars
            if (frame.Length >= 2)
            {
                var status = frame[..2].ToUpperInvariant();
                isStable = status == "ST";
            }

            // Detect sign
            int signMultiplier = 1;
            if (frame.Contains('-'))
                signMultiplier = -1;

            string weightPart = frame;

            // Apply custom substring config if configured
            if (WeightSubstringStart.HasValue && WeightSubstringLength.HasValue && WeightSubstringLength.Value > 0)
            {
                int start = WeightSubstringStart.Value;
                int len = WeightSubstringLength.Value;

                if (start >= 0 && start < frame.Length)
                {
                    if (start + len > frame.Length)
                    {
                        len = frame.Length - start;
                    }
                    weightPart = frame.Substring(start, len);
                }
            }

            // Extract numeric portion: strip all non-digit/non-dot characters
            var numericChars = new List<char>();
            bool foundDigit = false;
            bool foundDot = false;
            
            for (int i = 0; i < weightPart.Length; i++)
            {
                char c = weightPart[i];
                if (char.IsDigit(c))
                {
                    foundDigit = true;
                    numericChars.Add(c);
                }
                else if (c == '.' && foundDigit && !foundDot)
                {
                    foundDot = true;
                    numericChars.Add(c);
                }
                else if (foundDigit && !char.IsDigit(c) && c != '.')
                {
                    // Stop at unit text (e.g., "kg")
                    if (char.IsLetter(c)) break;
                }
            }

            if (numericChars.Count == 0) return null;

            var numericStr = new string(numericChars.ToArray());
            if (decimal.TryParse(numericStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var weight))
            {
                return weight * signMultiplier;
            }
        }
        catch
        {
            // Swallow parse errors — return null for malformed frames
        }

        return null;
    }

    /// <summary>
    /// Resets internal buffer (call when reconnecting or changing port).
    /// </summary>
    public void ResetBuffer() => _buffer = string.Empty;
}
