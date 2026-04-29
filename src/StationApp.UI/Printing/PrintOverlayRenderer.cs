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
            foreach (var field in template.Fields)
            {
                var value = values.GetValueOrDefault(field.FieldKey);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var text = new TextBlock
                {
                    Text = value,
                    Width = MmToDip(field.Width),
                    FontSize = field.FontSize,
                    FontWeight = ToFontWeight(field.FontWeight),
                    TextAlignment = ToTextAlignment(field.Alignment),
                    TextWrapping = field.WrapMode == PrintWrapMode.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    TextTrimming = field.WrapMode == PrintWrapMode.Trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                    Foreground = Brushes.Black,
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
