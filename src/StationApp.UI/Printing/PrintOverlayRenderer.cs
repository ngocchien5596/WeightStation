using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrintOverlayRenderer
{
    private static readonly FontFamily PrintFontFamily = new("Times New Roman");
    private static readonly Brush ShadedFieldBrush = new SolidColorBrush(Color.FromRgb(217, 217, 217));
    private const double PrintFontSizeBoost = 4d;

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
                    FontFamily = PrintFontFamily,
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
                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(field.LiteralValue))
                {
                    value = field.LiteralValue;
                }
                var isSelected = string.Equals(field.FieldKey, options.SelectedFieldKey, StringComparison.OrdinalIgnoreCase);

                if (field.IsLine)
                {
                    fixedPage.Children.Add(BuildHorizontalLine(field, options, previewMode, isSelected));
                    continue;
                }

                if (field.IsImage)
                {
                    if (previewMode)
                    {
                        fixedPage.Children.Add(BuildFieldOutline(field, options, isSelected));
                    }

                    var image = BuildImageElement(field, options);
                    if (image != null)
                    {
                        fixedPage.Children.Add(image);
                    }

                    continue;
                }

                if (previewMode)
                {
                    fixedPage.Children.Add(BuildFieldOutline(field, options, isSelected));
                }

                if (!previewMode && string.Equals(field.FieldKey, "Notes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value) && !previewMode)
                {
                    continue;
                }

                var text = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) && previewMode ? $"[{field.FieldKey}]" : value,
                    Width = MmToDip(field.Width),
                    FontFamily = PrintFontFamily,
                    FontSize = field.FontSize + PrintFontSizeBoost,
                    FontWeight = ToFontWeight(field.FontWeight),
                    FontStyle = field.Italic ? FontStyles.Italic : FontStyles.Normal,
                    TextAlignment = ToTextAlignment(field.Alignment),
                    TextWrapping = field.WrapMode == PrintWrapMode.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    TextTrimming = field.WrapMode == PrintWrapMode.Trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                    Foreground = string.IsNullOrWhiteSpace(value) && previewMode
                        ? new SolidColorBrush(Color.FromRgb(148, 163, 184))
                        : Brushes.Black,
                    Opacity = string.IsNullOrWhiteSpace(value) && previewMode ? 0.9 : 1,
                    Background = field.ShadedBackground ? ShadedFieldBrush : Brushes.Transparent
                };

                if (field.Underline)
                {
                    text.TextDecorations = TextDecorations.Underline;
                }

                if (field.MaxLines > 1)
                {
                    text.MaxHeight = field.MaxLines * (field.FontSize + PrintFontSizeBoost) * 1.4;
                }

                if (Math.Abs(field.RotationDegrees) > 0.001d)
                {
                    text.RenderTransform = new RotateTransform(field.RotationDegrees);
                    text.RenderTransformOrigin = new Point(0, 0);
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

    private static FrameworkElement? BuildImageElement(PrintFieldDefinition field, PrintOptionsModel options)
    {
        if (string.IsNullOrWhiteSpace(field.ImageSourceUri))
        {
            return null;
        }

        var image = new Image
        {
            Width = MmToDip(field.Width),
            Height = MmToDip(field.Width),
            Stretch = Stretch.Uniform,
            Source = new BitmapImage(new Uri(field.ImageSourceUri, UriKind.Absolute))
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        FixedPage.SetLeft(image, MmToDip(field.X + options.OffsetXmm));
        FixedPage.SetTop(image, MmToDip(field.Y + options.OffsetYmm));
        return image;
    }

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
                ? field with { X = position.X, Y = position.Y, Width = position.Width ?? field.Width }
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

    private static FrameworkElement BuildHorizontalLine(
        PrintFieldDefinition field,
        PrintOptionsModel options,
        bool previewMode,
        bool isSelected)
    {
        var line = new Border
        {
            Width = MmToDip(field.Width),
            Height = previewMode ? 2.2 : 1.2,
            Background = Brushes.Black,
            Opacity = previewMode ? 0.85 : 1
        };

        if (previewMode)
        {
            line.BorderBrush = isSelected
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                : new SolidColorBrush(Color.FromArgb(110, 148, 163, 184));
            line.BorderThickness = isSelected ? new Thickness(1.1) : new Thickness(0.6);
        }

        FixedPage.SetLeft(line, MmToDip(field.X + options.OffsetXmm));
        FixedPage.SetTop(line, MmToDip(field.Y + options.OffsetYmm));
        return line;
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
