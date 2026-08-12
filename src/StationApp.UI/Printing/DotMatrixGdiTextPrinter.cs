using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

internal static class DotMatrixGdiTextPrinter
{
    private const string DeliveryTicketA5V2ProfileKey = "delivery-pgn-ver-2-a5-mau-moi";
    private const double PrintUnitsPerMm = 100d / 25.4d;
    private const double WpfDipToPoint = 72d / 96d;
    private const double PrintFontSizeBoost = 4d;
    private const double NormalTextSecondStrikeOffsetMm = 0.06d;

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
        ApplyBestPrinterResolution(printDoc);

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
        var shouldPrintNotes = IsDeliveryTicketA5V2Template(template);
        var fields = ApplyFieldPositions(template.Fields, options.FieldPositions)
            .Where(x => x.IsEnabled && !x.IsImage && !x.IsLine)
            .OrderBy(x => x.Y)
            .ThenBy(x => x.X);

        foreach (var field in fields)
        {
            if (!shouldPrintNotes && string.Equals(field.FieldKey, "Notes", StringComparison.OrdinalIgnoreCase))
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

        if (!IsFinite(x) || !IsFinite(y) || !float.IsFinite(width) || !float.IsFinite(fontSizePoints))
        {
            return;
        }
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
        using var format = CreateStringFormat(field);

        var lineHeight = font.GetHeight(graphics);
        var height = Math.Max(
            (float)MmToPrintUnit(5.5d * Math.Max(1, field.MaxLines)),
            lineHeight * Math.Max(1, field.MaxLines) * 1.18f);

        if (!float.IsFinite(height) || height <= 0)
        {
            return;
        }

        var bounds = new RectangleF((float)x, (float)y, width, height);
        var text = SanitizeText(value);
        try
        {
            graphics.DrawString(text, font, Brushes.Black, bounds, format);
            DrawSecondStrikeIfNeeded(graphics, font, bounds, format, text, field);
        }
        catch (ArgumentException)
        {
            using var fallbackFormat = new StringFormat { Alignment = format.Alignment, LineAlignment = StringAlignment.Near };
            graphics.DrawString(text, font, Brushes.Black, new PointF(bounds.X, bounds.Y), fallbackFormat);
            DrawSecondStrikeIfNeeded(graphics, font, bounds, fallbackFormat, text, field);
        }
    }

    private static void DrawSecondStrikeIfNeeded(
        Graphics graphics,
        Font font,
        RectangleF bounds,
        StringFormat format,
        string text,
        PrintFieldDefinition field)
    {
        if (field.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold)
        {
            return;
        }

        var secondStrikeOffset = (float)MmToPrintUnit(NormalTextSecondStrikeOffsetMm);
        if (!float.IsFinite(secondStrikeOffset) || secondStrikeOffset <= 0)
        {
            return;
        }

        var secondBounds = bounds;
        secondBounds.X += secondStrikeOffset;
        graphics.DrawString(text, font, Brushes.Black, secondBounds, format);
    }

    private static StringFormat CreateStringFormat(PrintFieldDefinition field)
    {
        var format = new StringFormat
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

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void ApplyBestPrinterResolution(PrintDocument printDoc)
    {
        var bestResolution = printDoc.PrinterSettings.PrinterResolutions
            .Cast<PrinterResolution>()
            .Where(x => x.Kind != PrinterResolutionKind.Custom && x.X > 0 && x.Y > 0)
            .OrderByDescending(x => x.X * x.Y)
            .FirstOrDefault();

        if (bestResolution != null)
        {
            printDoc.DefaultPageSettings.PrinterResolution = bestResolution;
        }
    }

    private static string SanitizeText(string value)
        => value.Replace('\0', ' ').Replace("\r\n", "\n").Replace('\r', '\n');

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

    private static bool IsDeliveryTicketA5V2Template(PrintTemplateDefinition template)
        => template.Kind == PrintDocumentKind.DeliveryTicket
           && string.Equals(template.ActiveProfileKey, DeliveryTicketA5V2ProfileKey, StringComparison.OrdinalIgnoreCase);
}
