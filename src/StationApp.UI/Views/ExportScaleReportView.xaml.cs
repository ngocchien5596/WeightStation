using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StationApp.Application.DTOs;

namespace StationApp.UI.Views;

public partial class ExportScaleReportView : UserControl
{
    private bool _suppressLookupRefresh;

    public ExportScaleReportView()
    {
        InitializeComponent();
    }

    private void LookupComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        comboBox.ApplyTemplate();
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox editableTextBox)
        {
            return;
        }

        editableTextBox.TextChanged -= LookupEditableTextBox_TextChanged;
        editableTextBox.TextChanged += LookupEditableTextBox_TextChanged;
    }

    private void LookupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ReportLookupOptionDto option)
        {
            return;
        }

        comboBox.Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    _suppressLookupRefresh = true;
                    comboBox.Text = option.DisplayName;
                    comboBox.IsDropDownOpen = false;

                    if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox editableTextBox)
                    {
                        editableTextBox.CaretIndex = editableTextBox.Text.Length;
                        editableTextBox.SelectionLength = 0;
                    }
                }
                finally
                {
                    _suppressLookupRefresh = false;
                }
            },
            DispatcherPriority.ContextIdle);
    }

    private void LookupEditableTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressLookupRefresh)
        {
            return;
        }

        if (sender is not TextBox editableTextBox || !editableTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        if (FindParentComboBox(editableTextBox) is not { } comboBox)
        {
            return;
        }

        var caretIndex = editableTextBox.CaretIndex;
        comboBox.Dispatcher.BeginInvoke(
            () =>
            {
                comboBox.Items.Refresh();
                if (!comboBox.IsDropDownOpen && !string.IsNullOrWhiteSpace(editableTextBox.Text))
                {
                    comboBox.IsDropDownOpen = true;
                }

                editableTextBox.Focus();
                editableTextBox.CaretIndex = Math.Min(caretIndex, editableTextBox.Text.Length);
                editableTextBox.SelectionLength = 0;
            },
            DispatcherPriority.Background);
    }

    private static ComboBox? FindParentComboBox(DependencyObject child)
    {
        var current = child;
        while (current != null)
        {
            if (current is ComboBox comboBox)
            {
                return comboBox;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
