using System;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using StationApp.Application.Printing;

namespace StationApp.Infrastructure.Services;

public sealed class PrintDocumentExporter : IPrintDocumentExporter
{
    private readonly string _printingStationName;

    public PrintDocumentExporter(IConfiguration configuration)
    {
        var station = configuration["PrintingStationName"];
        if (string.IsNullOrWhiteSpace(station))
        {
            var machineName = Environment.MachineName.ToUpperInvariant();
            if (machineName.Contains("C6"))
            {
                station = "C6";
            }
            else if (machineName.Contains("C2"))
            {
                station = "C2";
            }
            else
            {
                station = "C2";
            }
        }
        _printingStationName = station.Trim();
    }
    private const double ExcelGridColumnMm = 2.5d;
    private const double ExcelGridRowMm = 3.8d;
    private const double WordGridColumnMm = 3d;
    private const double WordGridRowMm = 4.2d;
    private const double PrintFontSizeBoost = 4d;

    public Task ExportExcelAsync(
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        string outputPath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureDirectory(outputPath);

        using var workbook = new XLWorkbook();
        foreach (var page in batch.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var sheet = workbook.Worksheets.Add(GetSafeSheetName(page.DisplayNumber, workbook.Worksheets.Count + 1));
            BuildExcelPage(sheet, template, page);
        }

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    public Task ExportWordAsync(
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        string outputPath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureDirectory(outputPath);

        using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body!;

        for (var i = 0; i < batch.Pages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i > 0)
            {
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            body.Append(BuildWordPageTable(template, batch.Pages[i]));
        }

        body.Append(new SectionProperties(
            new PageSize
            {
                Width = ToTwips(template.PageWidthMm),
                Height = ToTwips(template.PageHeightMm)
            },
            new PageMargin
            {
                Top = 0,
                Right = 0,
                Bottom = 0,
                Left = 0,
                Header = 0,
                Footer = 0,
                Gutter = 0
            }));

        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    private void BuildExcelPage(IXLWorksheet sheet, PrintTemplateDefinition template, PrintPreviewPageModel page)
    {
        if (template.Kind == PrintDocumentKind.WeighTicket)
        {
            BuildWeighTicketExcelPage(sheet, template, page);
            return;
        }

        var columnCount = Math.Max(1, (int)Math.Ceiling(template.PageWidthMm / ExcelGridColumnMm));
        var rowCount = Math.Max(1, (int)Math.Ceiling(template.PageHeightMm / ExcelGridRowMm));
        var valuesByKey = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        sheet.Style.Font.FontName = "Times New Roman";
        sheet.PageSetup.PageOrientation = template.PageWidthMm > template.PageHeightMm
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;
        sheet.PageSetup.Margins.Top = 0;
        sheet.PageSetup.Margins.Bottom = 0;
        sheet.PageSetup.Margins.Left = 0;
        sheet.PageSetup.Margins.Right = 0;
        sheet.PageSetup.FitToPages(1, 1);

        for (var col = 1; col <= columnCount; col++)
        {
            sheet.Column(col).Width = 1.15;
        }

        for (var row = 1; row <= rowCount; row++)
        {
            sheet.Row(row).Height = 13.2;
        }

        var positionedFields = template.Fields
            .Where(x => x.IsEnabled)
            .Select(field => new ExcelFieldPlacement(
                field,
                Clamp((int)Math.Round(field.Y / ExcelGridRowMm) + 1, 1, rowCount),
                Clamp((int)Math.Round(field.X / ExcelGridColumnMm) + 1, 1, columnCount),
                Clamp((int)Math.Ceiling(field.Width / ExcelGridColumnMm), 1, columnCount)))
            .ToList();

        foreach (var placement in positionedFields.Where(x => x.Field.IsImage).OrderBy(x => x.Row).ThenBy(x => x.Column))
        {
            TryAddExcelImage(sheet, placement);
        }

        foreach (var placement in positionedFields.Where(x => !x.Field.IsImage).OrderBy(x => x.Row).ThenBy(x => x.Column))
        {
            var field = placement.Field;
            var row = placement.Row;
            var col = placement.Column;
            var value = ResolveFieldValue(field, valuesByKey);
            var colSpan = ResolveExcelColumnSpan(placement, positionedFields, value, columnCount);
            var rowSpan = Clamp(field.MaxLines, 1, rowCount - row + 1);

            if (field.IsLine)
            {
                var lineRange = sheet.Range(row, col, row, col + colSpan - 1);
                lineRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                lineRange.Style.Border.BottomBorderColor = XLColor.Black;
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var estimatedLines = EstimateExcelLineCount(value, colSpan);
            rowSpan = Clamp(Math.Max(rowSpan, Math.Min(estimatedLines, 4)), 1, rowCount - row + 1);

            var range = sheet.Range(row, col, row + rowSpan - 1, col + colSpan - 1);
            if (range.RowCount() > 1 || range.ColumnCount() > 1)
            {
                range.Merge();
            }

            var cell = sheet.Cell(row, col);
            cell.Value = value;
            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = field.FontSize + PrintFontSizeBoost;
            cell.Style.Font.Bold = field.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold;
            cell.Style.Font.Italic = field.Italic;
            cell.Style.Font.Underline = field.Underline ? XLFontUnderlineValues.Single : XLFontUnderlineValues.None;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.ShrinkToFit = false;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            cell.Style.Alignment.Horizontal = field.Alignment switch
            {
                PrintFieldAlignment.Center => XLAlignmentHorizontalValues.Center,
                PrintFieldAlignment.Right => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left
            };

            if (field.ShadedBackground)
            {
                range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            }

            var desiredHeight = Math.Max(sheet.Row(row).Height, (field.FontSize + PrintFontSizeBoost + 3d) * estimatedLines);
            sheet.Row(row).Height = Math.Min(90d, desiredHeight);
        }
    }

    private void BuildWeighTicketExcelPage(IXLWorksheet sheet, PrintTemplateDefinition template, PrintPreviewPageModel page)
    {
        var valuesByKey = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var fieldsByKey = template.Fields.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);

        sheet.Style.Font.FontName = "Times New Roman";
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.Margins.Top = 0;
        sheet.PageSetup.Margins.Bottom = 0;
        sheet.PageSetup.Margins.Left = 0;
        sheet.PageSetup.Margins.Right = 0;
        sheet.PageSetup.FitToPages(1, 1);
        sheet.PageSetup.PrintAreas.Add("A1:CE37");

        sheet.Column("A").Width = 2.5703125;
        sheet.Column("B").Width = 1.85546875;
        for (var col = 3; col <= 83; col++)
        {
            sheet.Column(col).Width = 1.15;
        }

        var rowHeights = new Dictionary<int, double>
        {
            [1] = 13.15, [2] = 13.15, [3] = 28.5, [5] = 16.9, [6] = 13.15,
            [7] = 33.2, [8] = 13.15, [9] = 32.85, [10] = 13.15, [11] = 13.15,
            [12] = 32.85, [13] = 13.15, [14] = 32.85, [15] = 13.15, [16] = 13.15,
            [17] = 13.15, [18] = 32.85, [19] = 13.15, [20] = 13.15, [21] = 33.2,
            [22] = 13.15, [23] = 13.15, [24] = 13.15, [25] = 18.75, [26] = 13.15,
            [27] = 13.15, [28] = 17.25, [29] = 13.15, [30] = 13.15, [31] = 13.15,
            [32] = 13.15, [33] = 13.15, [34] = 16.9, [35] = 13.15, [36] = 13.15,
            [37] = 28.35
        };
        for (var row = 1; row <= 37; row++)
        {
            sheet.Row(row).Height = rowHeights.GetValueOrDefault(row, 13.15);
        }

        foreach (var merge in WeighTicketExcelMerges)
        {
            sheet.Range(merge).Merge();
        }

        AddWeighTicketBorders(sheet);

        if (fieldsByKey.TryGetValue("StaticCompanyLogo", out var logoField))
        {
            TryAddWeighTicketLogo(sheet, logoField);
        }

        foreach (var (fieldKey, cellAddress) in WeighTicketExcelCellMap)
        {
            if (!fieldsByKey.TryGetValue(fieldKey, out var field) || !field.IsEnabled)
            {
                continue;
            }

            var value = ResolveFieldValue(field, valuesByKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var cell = sheet.Cell(cellAddress);
            cell.Value = value;
            ApplyExcelFieldStyle(cell, field);
        }
    }

    private static void AddWeighTicketBorders(IXLWorksheet sheet)
    {
        foreach (var rangeAddress in new[] { "G7:CE7", "G27:CE27", "G37:CE37" })
        {
            var range = sheet.Range(rangeAddress);
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorderColor = XLColor.Black;
        }
    }

    private static void TryAddWeighTicketLogo(IXLWorksheet sheet, PrintFieldDefinition logoField)
    {
        var path = ResolveImagePath(logoField.ImageSourceUri);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var imageBytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(imageBytes);
        sheet.AddPicture(stream, logoField.FieldKey)
            .MoveTo(sheet.Cell("F3"), 12, 7)
            .WithSize(59, 67);
    }

    private static void ApplyExcelFieldStyle(IXLCell cell, PrintFieldDefinition field)
    {
        cell.Style.Font.FontName = "Times New Roman";
        cell.Style.Font.FontSize = field.FontSize + PrintFontSizeBoost;
        cell.Style.Font.Bold = field.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold;
        cell.Style.Font.Italic = field.Italic;
        cell.Style.Font.Underline = field.Underline ? XLFontUnderlineValues.Single : XLFontUnderlineValues.None;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.ShrinkToFit = false;
        cell.Style.Alignment.Vertical = GetWeighTicketVerticalAlignment(field);
        cell.Style.Alignment.Horizontal = GetWeighTicketHorizontalAlignment(field);

        if (field.ShadedBackground)
        {
            var shadedRange = cell.MergedRange();
            if (shadedRange != null)
            {
                shadedRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            }
            else
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            }
        }
    }

    private static XLAlignmentVerticalValues GetWeighTicketVerticalAlignment(PrintFieldDefinition field)
        => field.FieldKey is "StaticProductLabel" or "ProductName" or "StaticFooterLeft" or "StaticFooterRight"
            ? XLAlignmentVerticalValues.Top
            : XLAlignmentVerticalValues.Center;

    private static XLAlignmentHorizontalValues GetWeighTicketHorizontalAlignment(PrintFieldDefinition field)
    {
        if (field.FieldKey is "StaticCompanyName" or "StaticCompanyAddress" or "StaticCompanyPhone")
        {
            return XLAlignmentHorizontalValues.Left;
        }

        return field.Alignment switch
        {
            PrintFieldAlignment.Center => XLAlignmentHorizontalValues.Center,
            PrintFieldAlignment.Right => XLAlignmentHorizontalValues.Right,
            _ => XLAlignmentHorizontalValues.Left
        };
    }

    private static void TryAddExcelImage(IXLWorksheet sheet, ExcelFieldPlacement placement)
    {
        var path = ResolveImagePath(placement.Field.ImageSourceUri);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var imageBytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(imageBytes);
        var imageSizePx = Math.Max(1, (int)Math.Round(placement.Field.Width * 96d / 25.4d));
        sheet.AddPicture(stream, placement.Field.FieldKey)
            .MoveTo(sheet.Cell(placement.Row, placement.Column))
            .WithSize(imageSizePx, imageSizePx);
    }

    private static int ResolveExcelColumnSpan(
        ExcelFieldPlacement placement,
        IReadOnlyList<ExcelFieldPlacement> placements,
        string value,
        int columnCount)
    {
        var baseSpan = Clamp(placement.ColumnSpan, 1, columnCount - placement.Column + 1);
        if (string.IsNullOrWhiteSpace(value))
        {
            return baseSpan;
        }

        var requiredSpan = Math.Max(baseSpan, EstimateRequiredExcelColumns(value, placement.Field));
        var nextColumn = placements
            .Where(other => !ReferenceEquals(other.Field, placement.Field)
                && !other.Field.IsLine
                && !other.Field.IsImage
                && RowsOverlap(placement.Row, placement.Row + Math.Max(placement.Field.MaxLines, 1) - 1, other.Row, other.Row + Math.Max(other.Field.MaxLines, 1) - 1)
                && other.Column > placement.Column)
            .Select(other => other.Column)
            .DefaultIfEmpty(columnCount + 1)
            .Min();

        if (ShouldReserveRightSideForWeighTicketField(placement.Field))
        {
            var rightSideStartColumn = Clamp((int)Math.Round(148d / ExcelGridColumnMm) + 1, placement.Column + baseSpan, columnCount + 1);
            nextColumn = Math.Max(nextColumn, rightSideStartColumn);
        }

        var gapBeforeNextField = ShouldReserveRightSideForWeighTicketField(placement.Field) ? 0 : 1;
        var maxSpanBeforeNextField = Math.Max(1, nextColumn - placement.Column - gapBeforeNextField);
        var maxSpan = Math.Min(columnCount - placement.Column + 1, Math.Max(baseSpan, maxSpanBeforeNextField));
        return Clamp(requiredSpan, baseSpan, maxSpan);
    }

    private static int EstimateRequiredExcelColumns(string value, PrintFieldDefinition field)
    {
        var longestLine = value
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x.Length)
            .DefaultIfEmpty(0)
            .Max();
        var fontSize = field.FontSize + PrintFontSizeBoost;
        var columnsPerChar = Math.Max(0.95d, fontSize / 13.5d);
        if (field.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold)
        {
            columnsPerChar *= 1.12d;
        }

        if (field.Italic)
        {
            columnsPerChar *= 1.05d;
        }

        return Math.Max(1, (int)Math.Ceiling(longestLine * columnsPerChar));
    }

    private static bool ShouldReserveRightSideForWeighTicketField(PrintFieldDefinition field)
        => field.FieldKey is
            "StaticCompanyName" or
            "StaticCompanyAddress" or
            "StaticCompanyPhone" or
            "StaticVehicleRegistrationLabel" or
            "VehicleRegistrationNo" or
            "StaticMoocRegistrationLabel" or
            "MoocRegistrationNo" or
            "StaticProductLabel" or
            "ProductName";

    private static int EstimateExcelLineCount(string value, int columnSpan)
    {
        var longestLine = value
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x.Length)
            .DefaultIfEmpty(0)
            .Max();
        var charsPerLine = Math.Max(6, (int)Math.Floor(columnSpan / 0.82d));
        return Math.Max(1, (int)Math.Ceiling(longestLine / (double)charsPerLine));
    }

    private Table BuildWordPageTable(PrintTemplateDefinition template, PrintPreviewPageModel page)
    {
        var columnCount = Math.Max(1, (int)Math.Ceiling(template.PageWidthMm / WordGridColumnMm));
        var rowCount = Math.Max(1, (int)Math.Ceiling(template.PageHeightMm / WordGridRowMm));
        var columnWidth = ToTwipsValue(WordGridColumnMm);
        var rowHeight = ToTwipsValue(WordGridRowMm);
        var valuesByKey = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var placementsByRow = BuildWordPlacements(template, valuesByKey, columnCount, rowCount);

        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Dxa, Width = (columnWidth * columnCount).ToString() },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));

        var grid = new TableGrid();
        for (var col = 0; col < columnCount; col++)
        {
            grid.Append(new GridColumn { Width = columnWidth.ToString() });
        }

        table.Append(grid);

        for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
        {
            var row = new TableRow(new TableRowProperties(
                new TableRowHeight { Val = (UInt32Value)(uint)rowHeight, HeightType = HeightRuleValues.Exact }));
            var placements = placementsByRow.GetValueOrDefault(rowIndex) ?? [];
            var currentCol = 1;

            foreach (var placement in placements.OrderBy(x => x.Column))
            {
                if (placement.Column > currentCol)
                {
                    row.Append(CreateWordCell(string.Empty, placement.Column - currentCol, null));
                }

                row.Append(CreateWordCell(placement.Value, placement.ColumnSpan, placement.Field));
                currentCol = placement.Column + placement.ColumnSpan;
            }

            if (currentCol <= columnCount)
            {
                row.Append(CreateWordCell(string.Empty, columnCount - currentCol + 1, null));
            }

            table.Append(row);
        }

        return table;
    }

    private Dictionary<int, List<WordPlacement>> BuildWordPlacements(
        PrintTemplateDefinition template,
        IReadOnlyDictionary<string, string> valuesByKey,
        int columnCount,
        int rowCount)
    {
        var placements = new Dictionary<int, List<WordPlacement>>();
        foreach (var field in template.Fields.Where(x => x.IsEnabled).OrderBy(x => x.Y).ThenBy(x => x.X))
        {
            var row = Clamp((int)Math.Round(field.Y / WordGridRowMm) + 1, 1, rowCount);
            var col = Clamp((int)Math.Round(field.X / WordGridColumnMm) + 1, 1, columnCount);
            var colSpan = Clamp((int)Math.Ceiling(field.Width / WordGridColumnMm), 1, columnCount - col + 1);

            var value = field.IsLine
                ? new string('_', Math.Max(3, colSpan * 2))
                : ResolveFieldValue(field, valuesByKey);

            if (field.IsImage || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!placements.TryGetValue(row, out var rowPlacements))
            {
                rowPlacements = [];
                placements[row] = rowPlacements;
            }

            if (rowPlacements.Any(x => RangesOverlap(col, col + colSpan - 1, x.Column, x.Column + x.ColumnSpan - 1)))
            {
                continue;
            }

            rowPlacements.Add(new WordPlacement(row, col, colSpan, value, field));
        }

        return placements;
    }

    private static TableCell CreateWordCell(string text, int columnSpan, PrintFieldDefinition? field)
    {
        var cellProperties = new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = ToTwips(WordGridColumnMm * columnSpan).ToString() },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top });

        if (columnSpan > 1)
        {
            cellProperties.Append(new GridSpan { Val = columnSpan });
        }

        if (field?.ShadedBackground == true)
        {
            cellProperties.Append(new Shading { Fill = "D9D9D9", Val = ShadingPatternValues.Clear });
        }

        return new TableCell(cellProperties, CreateWordParagraph(text, field));
    }

    private static Paragraph CreateWordParagraph(string text, PrintFieldDefinition? field)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0", Line = "220", LineRule = LineSpacingRuleValues.Auto });

        if (field != null)
        {
            paragraphProperties.Append(new Justification
            {
                Val = field.Alignment switch
                {
                    PrintFieldAlignment.Center => JustificationValues.Center,
                    PrintFieldAlignment.Right => JustificationValues.Right,
                    _ => JustificationValues.Left
                }
            });
        }

        var runProperties = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
            new FontSize { Val = ToHalfPointString((field?.FontSize ?? 10d) + PrintFontSizeBoost) });

        if (field?.FontWeight is PrintFieldWeight.Bold or PrintFieldWeight.SemiBold)
        {
            runProperties.Append(new Bold());
        }

        if (field?.Italic == true)
        {
            runProperties.Append(new Italic());
        }

        if (field?.Underline == true)
        {
            runProperties.Append(new Underline { Val = UnderlineValues.Single });
        }

        return new Paragraph(paragraphProperties, new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private string ResolveFieldValue(PrintFieldDefinition field, IReadOnlyDictionary<string, string> valuesByKey)
    {
        if (!string.IsNullOrWhiteSpace(field.LiteralValue))
        {
            if (field.FieldKey == "StaticFooterLeft" && field.LiteralValue.Contains("- C2"))
            {
                return field.LiteralValue.Replace("C2", _printingStationName);
            }
            return field.LiteralValue;
        }

        return valuesByKey.TryGetValue(field.FieldKey, out var value) ? value : string.Empty;
    }

    private static void EnsureDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string? ResolveImagePath(string? imageSourceUri)
    {
        if (string.IsNullOrWhiteSpace(imageSourceUri))
        {
            return null;
        }

        if (File.Exists(imageSourceUri))
        {
            return imageSourceUri;
        }

        var normalized = imageSourceUri.Trim();
        const string packPrefix = "pack://application:,,,/";
        if (normalized.StartsWith(packPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[packPrefix.Length..];
            var componentMarker = ";component/";
            var componentIndex = normalized.IndexOf(componentMarker, StringComparison.OrdinalIgnoreCase);
            if (componentIndex >= 0)
            {
                normalized = normalized[(componentIndex + componentMarker.Length)..];
            }
        }

        normalized = normalized.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        foreach (var basePath in EnumerateImageSearchRoots())
        {
            var candidate = Path.Combine(basePath, normalized);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateImageSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (seen.Add(current))
            {
                yield return current;
                yield return Path.Combine(current, "src", "StationApp.UI");
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }
    }

    private static string GetSafeSheetName(string value, int fallbackIndex)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe = new string((string.IsNullOrWhiteSpace(value) ? $"Phieu{fallbackIndex}" : value)
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = $"Phieu{fallbackIndex}";
        }

        return safe.Length <= 31 ? safe : safe[..31];
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    private static bool RangesOverlap(int start1, int end1, int start2, int end2) => start1 <= end2 && start2 <= end1;
    private static bool RowsOverlap(int start1, int end1, int start2, int end2) => start1 <= end2 && start2 <= end1;
    private static UInt32Value ToTwips(double mm) => (uint)ToTwipsValue(mm);
    private static int ToTwipsValue(double mm) => (int)Math.Max(1, Math.Round(mm * 56.6929133858d));
    private static string ToHalfPointString(double points) => Math.Max(1, (int)Math.Round(points * 2d)).ToString();

    private static readonly string[] WeighTicketExcelMerges =
    [
        "L3:AR3", "BI3:BX3", "L4:BO4", "L5:BE5", "BF5:BM5", "BN5:BY5",
        "G7:O7", "Q7:AH7", "BH7:BM7", "BO7:CE7",
        "BI8:BM8", "BO8:CE8",
        "G9:O9", "Q9:AH9",
        "G12:O12", "Q12:AH12", "AW12:BM12", "BO12:BU12",
        "G14:O14", "Q14:AV16", "AW14:BM14", "BO14:BS14",
        "G18:O18", "Q18:AF18", "AW18:BM18", "BO18:BU18",
        "G21:O22", "Q21:BE22", "BG21:BM21", "BO21:CC21",
        "G25:O25", "Q25:AV25", "BA25:BM25", "BO25:CE25",
        "J28:Z28", "AF28:AM28", "AW28:BG28", "BM28:CA28",
        "BK34:CA34", "G37:S37", "AR37:CD37"
    ];

    private static readonly (string FieldKey, string CellAddress)[] WeighTicketExcelCellMap =
    [
        ("StaticCompanyName", "L3"),
        ("StaticCompanyAddress", "L4"),
        ("StaticCompanyPhone", "L5"),
        ("StaticTitle", "BI3"),
        ("StaticTicketLabel", "BF5"),
        ("TicketNo", "BN5"),
        ("StaticVehiclePlateLabel", "G7"),
        ("VehiclePlate", "Q7"),
        ("StaticWeight1Label", "BH7"),
        ("Weight1DateTime", "BO7"),
        ("StaticVehicleRegistrationLabel", "G9"),
        ("VehicleRegistrationNo", "Q9"),
        ("StaticWeight2Label", "BI8"),
        ("Weight2DateTime", "BO8"),
        ("StaticMoocRegistrationLabel", "G12"),
        ("MoocRegistrationNo", "Q12"),
        ("StaticGrossWeightLabel", "AW12"),
        ("GrossWeight", "BO12"),
        ("StaticProductLabel", "G14"),
        ("ProductName", "Q14"),
        ("StaticEmptyWeightLabel", "AW14"),
        ("EmptyWeight", "BO14"),
        ("StaticLotNoLabel", "G18"),
        ("LotNo", "Q18"),
        ("StaticNetWeightLabel", "AW18"),
        ("NetWeight", "BO18"),
        ("StaticCustomerLabel", "G21"),
        ("CustomerName", "Q21"),
        ("StaticNotesLabel", "BG21"),
        ("Notes", "BO21"),
        ("StaticRepresentativeLabel", "G25"),
        ("RepresentativeName", "Q25"),
        ("StaticPrintedAtLabel", "BA25"),
        ("PrintedAt", "BO25"),
        ("StaticSigner1", "J28"),
        ("StaticSigner2", "AF28"),
        ("StaticSigner3", "AW28"),
        ("StaticSigner4", "BM28"),
        ("PrintedBy", "BK34"),
        ("StaticFooterLeft", "G37"),
        ("StaticFooterRight", "AR37")
    ];

    private sealed record ExcelFieldPlacement(PrintFieldDefinition Field, int Row, int Column, int ColumnSpan);
    private sealed record WordPlacement(int Row, int Column, int ColumnSpan, string Value, PrintFieldDefinition Field);
}
