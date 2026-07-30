using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

internal static class DotMatrixGdiTextPrinter
{
    private const double PrintUnitsPerMm = 100d / 25.4d;
    private const double WpfDipToPoint = 72d / 96d;
    private const double PrintFontSizeBoost = 4d;

    public static void Print(
        string printerName,
        string jobName,
        PrintTemplateDefinition template,
        PrintPreviewPageModel page,
        PrintOptionsModel options)
    {
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;
        printDoc.DocumentName = jobName;
        printDoc.OriginAtMargins = false;
        printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        var paperWidth = (int)Math.Round(template.PageWidthMm * PrintUnitsPerMm);
        var paperHeight = (int)Math.Round(template.PageHeightMm * PrintUnitsPerMm);
        printDoc.DefaultPageSettings.PaperSize = new PaperSize("Custom", paperWidth, paperHeight);

        printDoc.PrintPage += (_, e) =>
        {
            if (e.Graphics == null)
            {
                return;
            }

            DrawPage(e.Graphics, e.PageBounds, e.PageSettings, template, page, options);
            e.HasMorePages = false;
        };

        printDoc.Print();
    }

    private static void DrawPage(
        Graphics graphics,
        Rectangle pageBounds,
        PageSettings pageSettings,
        PrintTemplateDefinition template,
        PrintPreviewPageModel page,
        PrintOptionsModel options)
    {
        graphics.PageUnit = GraphicsUnit.Display;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var targetWidth = template.PageWidthMm * PrintUnitsPerMm;
        var targetHeight = template.PageHeightMm * PrintUnitsPerMm;
        var availablePhysicalHeight = template.PageHeightMm <= 150.0 && pageBounds.Height > 800
            ? pageBounds.Height / 2.0
            : pageBounds.Height;

        var pageX = Math.Max(0.0, (pageBounds.Width - targetWidth) / 2.0) - pageSettings.HardMarginX;
        var pageY = Math.Max(0.0, (availablePhysicalHeight - targetHeight) / 2.0) - pageSettings.HardMarginY;

        var values = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var fields = ApplyFieldPositions(template.Fields, options.FieldPositions)
            .Where(x => x.IsEnabled && !x.IsImage && !x.IsLine)
            .OrderBy(x => x.Y)
            .ThenBy(x => x.X);

        foreach (var field in fields)
        {
            if (string.Equals(field.FieldKey, "Notes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!values.TryGetValue(field.FieldKey, out var value) || string.IsNullOrWhiteSpace(value))
            {
                value = field.LiteralValue ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            DrawField(graphics, pageX, pageY, field, value, options);
        }
    }

    private static void DrawField(
        Graphics graphics,
        double pageX,
        double pageY,
        PrintFieldDefinition field,
        string value,
        PrintOptionsModel options)
    {
        var x = pageX + MmToPrintUnit(field.X + options.OffsetXmm);
        var y = pageY + MmToPrintUnit(field.Y + options.OffsetYmm);
        var width = Math.Max(1f, (float)MmToPrintUnit(field.Width));
        var fontSizePoints = Math.Max(1f, (float)((field.FontSize + PrintFontSizeBoost) * WpfDipToPoint));
        var style = FontStyle.Regular;

        if (field.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold)
        {
            style |= FontStyle.Bold;
        }

        if (field.Italic)
        {
            style |= FontStyle.Italic;
        }

        if (field.Underline)
        {
            style |= FontStyle.Underline;
        }

        using var font = new Font("Times New Roman", fontSizePoints, style, GraphicsUnit.Point);
        using var brush = Brushes.Black;
        using var format = CreateStringFormat(field);

        var lineHeight = font.GetHeight(graphics);
        var height = Math.Max(
            (float)MmToPrintUnit(5.5d * Math.Max(1, field.MaxLines)),
            lineHeight * Math.Max(1, field.MaxLines) * 1.18f);

        var bounds = new RectangleF((float)x, (float)y, width, height);
        graphics.DrawString(value, font, brush, bounds, format);
    }

    private static StringFormat CreateStringFormat(PrintFieldDefinition field)
    {
        var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = field.Alignment switch
            {
                PrintFieldAlignment.Center => StringAlignment.Center,
                PrintFieldAlignment.Right => StringAlignment.Far,
                _ => StringAlignment.Near
            },
            LineAlignment = StringAlignment.Near,
            Trimming = field.WrapMode == PrintWrapMode.Trim
                ? StringTrimming.EllipsisCharacter
                : StringTrimming.None
        };

        if (field.WrapMode != PrintWrapMode.Wrap)
        {
            format.FormatFlags |= StringFormatFlags.NoWrap;
        }

        return format;
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

    private static double MmToPrintUnit(double mm) => mm * PrintUnitsPerMm;
}
