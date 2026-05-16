using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrintOverlayRenderer
{
    public FixedDocument CreateDocument(
        PrintTemplateDefinition template,
        IReadOnlyList<PrintPreviewPageModel> pages,
        PrintOptionsModel options,
        bool previewMode)
    {
        var document = new FixedDocument();
        var pageWidth = MmToDip(template.PageWidthMm);
        var pageHeight = MmToDip(template.PageHeightMm);

        foreach (var page in pages)
        {
            var fixedPage = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            if (previewMode)
            {
                fixedPage.Children.Add(new Border
                {
                    Width = pageWidth - 8,
                    Height = pageHeight - 8,
                    Margin = new Thickness(4),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
                });

                fixedPage.Children.Add(new TextBlock
                {
                    Text = $"PRE-PRINTED OVERLAY PREVIEW · {template.TemplateName} · {page.DisplayNumber}",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(12, 8, 12, 0)
                });
            }

            var values = page.Fields.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var positionedFields = ApplyFieldPositions(template.Fields, options.FieldPositions);

            foreach (var field in positionedFields)
            {
                var value = values.GetValueOrDefault(field.FieldKey);
                var isSelected = string.Equals(field.FieldKey, options.SelectedFieldKey, StringComparison.OrdinalIgnoreCase);

                if (previewMode)
                {
                    fixedPage.Children.Add(BuildFieldOutline(field, options, isSelected));
                }

                if (string.IsNullOrWhiteSpace(value) && !previewMode)
                {
                    continue;
                }

                var text = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) && previewMode ? $"[{field.FieldKey}]" : value,
                    Width = MmToDip(field.Width),
                    FontSize = field.FontSize,
                    FontWeight = ToFontWeight(field.FontWeight),
                    TextAlignment = ToTextAlignment(field.Alignment),
                    TextWrapping = field.WrapMode == PrintWrapMode.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    TextTrimming = field.WrapMode == PrintWrapMode.Trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                    Foreground = string.IsNullOrWhiteSpace(value) && previewMode
                        ? new SolidColorBrush(Color.FromRgb(148, 163, 184))
                        : Brushes.Black,
                    Opacity = string.IsNullOrWhiteSpace(value) && previewMode ? 0.9 : 1,
                    Background = Brushes.Transparent
                };

                if (field.MaxLines > 1)
                {
                    text.MaxHeight = field.MaxLines * field.FontSize * 1.4;
                }

                FixedPage.SetLeft(text, MmToDip(field.X + options.OffsetXmm));
                FixedPage.SetTop(text, MmToDip(field.Y + options.OffsetYmm));
                fixedPage.Children.Add(text);
            }

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static double MmToDip(double mm) => mm * 96d / 25.4d;

    private static IReadOnlyList<PrintFieldDefinition> ApplyFieldPositions(
        IReadOnlyList<PrintFieldDefinition> fields,
        IReadOnlyList<PrintFieldPosition> positions)
    {
        if (positions.Count == 0)
        {
            return fields;
        }

        var map = positions.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        return fields
            .Select(field => map.TryGetValue(field.FieldKey, out var position)
                ? field with { X = position.X, Y = position.Y }
                : field)
            .ToList();
    }

    private static FrameworkElement BuildFieldOutline(PrintFieldDefinition field, PrintOptionsModel options, bool isSelected)
    {
        var border = new Border
        {
            Width = MmToDip(field.Width),
            Height = Math.Max(MmToDip(5.5d * field.MaxLines), field.FontSize * 1.9 * field.MaxLines),
            BorderBrush = isSelected
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                : new SolidColorBrush(Color.FromArgb(110, 148, 163, 184)),
            BorderThickness = isSelected ? new Thickness(1.4) : new Thickness(0.8),
            Background = isSelected
                ? new SolidColorBrush(Color.FromArgb(20, 239, 68, 68))
                : new SolidColorBrush(Color.FromArgb(10, 148, 163, 184))
        };

        FixedPage.SetLeft(border, MmToDip(field.X + options.OffsetXmm));
        FixedPage.SetTop(border, MmToDip(field.Y + options.OffsetYmm));
        return border;
    }

    private static TextAlignment ToTextAlignment(PrintFieldAlignment alignment) => alignment switch
    {
        PrintFieldAlignment.Center => TextAlignment.Center,
        PrintFieldAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Left
    };

    private static FontWeight ToFontWeight(PrintFieldWeight weight) => weight switch
    {
        PrintFieldWeight.Bold => FontWeights.Bold,
        PrintFieldWeight.SemiBold => FontWeights.SemiBold,
        _ => FontWeights.Normal
    };
}
