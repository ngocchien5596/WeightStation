using System.Globalization;
using System.Text;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

internal static class DotMatrixRawDocumentBuilder
{
    private const double CharactersPerInch = 10d;
    private const double LinesPerInch = 6d;

    public static byte[] Build(PrintTemplateDefinition template, PrintPreviewPageModel page, PrintOptionsModel options)
    {
        var text = BuildText(template, page, options);
        var bytes = new List<byte>(text.Length + 16)
        {
            0x1B, 0x40, // ESC @: initialize printer
            0x1B, 0x50, // ESC P: 10 CPI
            0x1B, 0x32  // ESC 2: 1/6 inch line spacing
        };

        bytes.AddRange(Encoding.ASCII.GetBytes(text));
        bytes.Add(0x0C); // Form feed
        return bytes.ToArray();
    }

    private static string BuildText(PrintTemplateDefinition template, PrintPreviewPageModel page, PrintOptionsModel options)
    {
        var columnCount = Math.Max(80, (int)Math.Floor(template.PageWidthMm / 25.4d * CharactersPerInch));
        var rowCount = Math.Max(36, (int)Math.Ceiling(template.PageHeightMm / 25.4d * LinesPerInch));
        var lines = Enumerable.Range(0, rowCount)
            .Select(_ => new char[columnCount])
            .ToArray();

        foreach (var line in lines)
        {
            Array.Fill(line, ' ');
        }

        var values = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var fields = ApplyFieldPositions(template.Fields, options.FieldPositions)
            .Where(x => x.IsEnabled && !x.IsImage && !x.IsLine)
            .OrderBy(x => x.Y)
            .ThenBy(x => x.X);

        foreach (var field in fields)
        {
            if (!values.TryGetValue(field.FieldKey, out var value) && string.IsNullOrWhiteSpace(field.LiteralValue))
            {
                continue;
            }

            value = string.IsNullOrWhiteSpace(value) ? field.LiteralValue ?? string.Empty : value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            WriteField(lines, field, value, options);
        }

        return string.Join("\r\n", lines.Select(TrimRightPreserveEmpty));
    }

    private static void WriteField(char[][] lines, PrintFieldDefinition field, string value, PrintOptionsModel options)
    {
        var row = MmToRow(field.Y + options.OffsetYmm);
        var column = MmToColumn(field.X + options.OffsetXmm);
        var width = Math.Max(1, MmToColumn(field.Width));
        var maxLines = Math.Max(1, field.MaxLines);

        if (row < 0 || row >= lines.Length || column >= lines[row].Length)
        {
            return;
        }

        var segments = WrapValue(NormalizeForPrinter(value), width, maxLines, field.WrapMode);
        for (var index = 0; index < segments.Count && row + index < lines.Length; index++)
        {
            var aligned = Align(segments[index], width, field.Alignment);
            WriteText(lines[row + index], column, aligned);
        }
    }

    private static IReadOnlyList<string> WrapValue(string value, int width, int maxLines, PrintWrapMode wrapMode)
    {
        var sourceLines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<string>();

        foreach (var source in sourceLines)
        {
            var remaining = source.Trim();
            if (wrapMode != PrintWrapMode.Wrap)
            {
                result.Add(TrimToWidth(remaining, width));
                if (result.Count >= maxLines)
                {
                    return result;
                }

                continue;
            }

            while (remaining.Length > width)
            {
                var splitAt = remaining.LastIndexOf(' ', Math.Min(width, remaining.Length - 1));
                if (splitAt <= 0)
                {
                    splitAt = width;
                }

                result.Add(remaining[..splitAt].TrimEnd());
                remaining = remaining[splitAt..].TrimStart();
                if (result.Count >= maxLines)
                {
                    return result;
                }
            }

            result.Add(remaining);
            if (result.Count >= maxLines)
            {
                return result;
            }
        }

        return result;
    }

    private static string Align(string text, int width, PrintFieldAlignment alignment)
    {
        text = TrimToWidth(text, width);
        return alignment switch
        {
            PrintFieldAlignment.Center => text.PadLeft(text.Length + Math.Max(0, (width - text.Length) / 2)).PadRight(width),
            PrintFieldAlignment.Right => text.PadLeft(width),
            _ => text.PadRight(width)
        };
    }

    private static void WriteText(char[] line, int column, string text)
    {
        if (column < 0)
        {
            text = text[Math.Min(text.Length, -column)..];
            column = 0;
        }

        for (var i = 0; i < text.Length && column + i < line.Length; i++)
        {
            line[column + i] = text[i];
        }
    }

    private static IReadOnlyList<PrintFieldDefinition> ApplyFieldPositions(
        IReadOnlyList<PrintFieldDefinition> fields,
        IReadOnlyList<PrintFieldPosition> positions)
    {
        if (positions.Count == 0)
        {
            return fields;
        }

        var overrides = positions.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        return fields
            .Select(field => overrides.TryGetValue(field.FieldKey, out var position)
                ? field with
                {
                    X = position.X,
                    Y = position.Y,
                    Width = position.Width ?? field.Width,
                    IsEnabled = position.IsEnabled
                }
                : field)
            .ToList();
    }

    private static int MmToColumn(double mm) => (int)Math.Round(mm / 25.4d * CharactersPerInch);
    private static int MmToRow(double mm) => (int)Math.Round(mm / 25.4d * LinesPerInch);
    private static string TrimToWidth(string value, int width) => value.Length <= width ? value : value[..width];
    private static string TrimRightPreserveEmpty(char[] line) => new string(line).TrimEnd();

    private static string NormalizeForPrinter(string value)
    {
        var normalized = ReplaceVietnameseCharacters(value.Replace('\t', ' ')).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var ascii = character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'D',
                _ => character
            };

            builder.Append(ascii <= 0x7F ? ascii : ' ');
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string ReplaceVietnameseCharacters(string value)
    {
        return value
            .Replace('\u00E0', 'a').Replace('\u00E1', 'a').Replace('\u1EA3', 'a').Replace('\u00E3', 'a').Replace('\u1EA1', 'a')
            .Replace('\u0103', 'a').Replace('\u1EB1', 'a').Replace('\u1EAF', 'a').Replace('\u1EB3', 'a').Replace('\u1EB5', 'a').Replace('\u1EB7', 'a')
            .Replace('\u00E2', 'a').Replace('\u1EA7', 'a').Replace('\u1EA5', 'a').Replace('\u1EA9', 'a').Replace('\u1EAB', 'a').Replace('\u1EAD', 'a')
            .Replace('\u00E8', 'e').Replace('\u00E9', 'e').Replace('\u1EBB', 'e').Replace('\u1EBD', 'e').Replace('\u1EB9', 'e')
            .Replace('\u00EA', 'e').Replace('\u1EC1', 'e').Replace('\u1EBF', 'e').Replace('\u1EC3', 'e').Replace('\u1EC5', 'e').Replace('\u1EC7', 'e')
            .Replace('\u00EC', 'i').Replace('\u00ED', 'i').Replace('\u1EC9', 'i').Replace('\u0129', 'i').Replace('\u1ECB', 'i')
            .Replace('\u00F2', 'o').Replace('\u00F3', 'o').Replace('\u1ECF', 'o').Replace('\u00F5', 'o').Replace('\u1ECD', 'o')
            .Replace('\u00F4', 'o').Replace('\u1ED3', 'o').Replace('\u1ED1', 'o').Replace('\u1ED5', 'o').Replace('\u1ED7', 'o').Replace('\u1ED9', 'o')
            .Replace('\u01A1', 'o').Replace('\u1EDD', 'o').Replace('\u1EDB', 'o').Replace('\u1EDF', 'o').Replace('\u1EE1', 'o').Replace('\u1EE3', 'o')
            .Replace('\u00F9', 'u').Replace('\u00FA', 'u').Replace('\u1EE7', 'u').Replace('\u0169', 'u').Replace('\u1EE5', 'u')
            .Replace('\u01B0', 'u').Replace('\u1EEB', 'u').Replace('\u1EE9', 'u').Replace('\u1EED', 'u').Replace('\u1EEF', 'u').Replace('\u1EF1', 'u')
            .Replace('\u1EF3', 'y').Replace('\u00FD', 'y').Replace('\u1EF7', 'y').Replace('\u1EF9', 'y').Replace('\u1EF5', 'y')
            .Replace('\u00C0', 'A').Replace('\u00C1', 'A').Replace('\u1EA2', 'A').Replace('\u00C3', 'A').Replace('\u1EA0', 'A')
            .Replace('\u0102', 'A').Replace('\u1EB0', 'A').Replace('\u1EAE', 'A').Replace('\u1EB2', 'A').Replace('\u1EB4', 'A').Replace('\u1EB6', 'A')
            .Replace('\u00C2', 'A').Replace('\u1EA6', 'A').Replace('\u1EA4', 'A').Replace('\u1EA8', 'A').Replace('\u1EAA', 'A').Replace('\u1EAC', 'A')
            .Replace('\u00C8', 'E').Replace('\u00C9', 'E').Replace('\u1EBA', 'E').Replace('\u1EBC', 'E').Replace('\u1EB8', 'E')
            .Replace('\u00CA', 'E').Replace('\u1EC0', 'E').Replace('\u1EBE', 'E').Replace('\u1EC2', 'E').Replace('\u1EC4', 'E').Replace('\u1EC6', 'E')
            .Replace('\u00CC', 'I').Replace('\u00CD', 'I').Replace('\u1EC8', 'I').Replace('\u0128', 'I').Replace('\u1ECA', 'I')
            .Replace('\u00D2', 'O').Replace('\u00D3', 'O').Replace('\u1ECE', 'O').Replace('\u00D5', 'O').Replace('\u1ECC', 'O')
            .Replace('\u00D4', 'O').Replace('\u1ED2', 'O').Replace('\u1ED0', 'O').Replace('\u1ED4', 'O').Replace('\u1ED6', 'O').Replace('\u1ED8', 'O')
            .Replace('\u01A0', 'O').Replace('\u1EDC', 'O').Replace('\u1EDA', 'O').Replace('\u1EDE', 'O').Replace('\u1EE0', 'O').Replace('\u1EE2', 'O')
            .Replace('\u00D9', 'U').Replace('\u00DA', 'U').Replace('\u1EE6', 'U').Replace('\u0168', 'U').Replace('\u1EE4', 'U')
            .Replace('\u01AF', 'U').Replace('\u1EEA', 'U').Replace('\u1EE8', 'U').Replace('\u1EEC', 'U').Replace('\u1EEE', 'U').Replace('\u1EF0', 'U')
            .Replace('\u1EF2', 'Y').Replace('\u00DD', 'Y').Replace('\u1EF6', 'Y').Replace('\u1EF8', 'Y').Replace('\u1EF4', 'Y')
            .Replace('\u0111', 'd').Replace('\u0110', 'D');
    }
}
