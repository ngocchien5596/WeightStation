using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace StationApp.UI.Helpers;

public static class DataGridCellCopyBehavior
{
    public static readonly DependencyProperty EnableCurrentCellCopyProperty =
        DependencyProperty.RegisterAttached(
            "EnableCurrentCellCopy",
            typeof(bool),
            typeof(DataGridCellCopyBehavior),
            new PropertyMetadata(false, OnEnableCurrentCellCopyChanged));

    public static bool GetEnableCurrentCellCopy(DependencyObject obj) => (bool)obj.GetValue(EnableCurrentCellCopyProperty);

    public static void SetEnableCurrentCellCopy(DependencyObject obj, bool value) => obj.SetValue(EnableCurrentCellCopyProperty, value);

    private static void OnEnableCurrentCellCopyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid)
        {
            return;
        }

        dataGrid.PreviewKeyDown -= OnPreviewKeyDown;

        if (e.NewValue is true)
        {
            dataGrid.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        if (!IsCopyGesture(e) || IsEditableTextInput(e.OriginalSource))
        {
            return;
        }

        if (!TryGetCurrentCellText(dataGrid, out var text))
        {
            return;
        }

        Clipboard.SetText(text);
        e.Handled = true;
    }

    private static bool IsCopyGesture(KeyEventArgs e) =>
        (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
        (e.Key == Key.C || e.Key == Key.Insert);

    private static bool IsEditableTextInput(object? originalSource) =>
        originalSource is TextBoxBase or PasswordBox or ComboBox;

    private static bool TryGetCurrentCellText(DataGrid dataGrid, out string text)
    {
        text = string.Empty;

        var currentCell = dataGrid.CurrentCell;
        if (currentCell.Column is null || currentCell.Item is null || currentCell.Item == CollectionView.NewItemPlaceholder)
        {
            return false;
        }

        if (TryReadFromVisual(currentCell.Column, currentCell.Item, out text))
        {
            return true;
        }

        if (TryReadFromBinding(currentCell.Column, currentCell.Item, out text))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadFromVisual(DataGridColumn column, object item, out string text)
    {
        text = string.Empty;

        if (column.GetCellContent(item) is not FrameworkElement content)
        {
            return false;
        }

        text = ExtractDisplayText(content);
        return true;
    }

    private static string ExtractDisplayText(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock:
                return textBlock.Text ?? string.Empty;
            case TextBox textBox:
                return textBox.Text ?? string.Empty;
            case CheckBox checkBox:
                return checkBox.IsChecked?.ToString() ?? string.Empty;
            case ContentPresenter contentPresenter when contentPresenter.Content is not null:
                return contentPresenter.Content.ToString() ?? string.Empty;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(element);
        for (var index = 0; index < childCount; index++)
        {
            var childText = ExtractDisplayText(VisualTreeHelper.GetChild(element, index));
            if (!string.IsNullOrWhiteSpace(childText))
            {
                return childText;
            }
        }

        return string.Empty;
    }

    private static bool TryReadFromBinding(DataGridColumn column, object item, out string text)
    {
        text = string.Empty;

        if (column is not DataGridBoundColumn boundColumn || boundColumn.Binding is not Binding binding)
        {
            return false;
        }

        var rawValue = EvaluateBindingPath(item, binding.Path?.Path);
        if (rawValue is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(binding.StringFormat))
        {
            text = FormatValue(rawValue, binding.StringFormat);
            return true;
        }

        text = rawValue.ToString() ?? string.Empty;
        return true;
    }

    private static object? EvaluateBindingPath(object source, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return source;
        }

        object? current = source;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private static string FormatValue(object value, string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return value.ToString() ?? string.Empty;
        }

        if (format.Contains('{'))
        {
            return string.Format(CultureInfo.CurrentCulture, format, value);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, CultureInfo.CurrentCulture);
        }

        return value.ToString() ?? string.Empty;
    }
}
