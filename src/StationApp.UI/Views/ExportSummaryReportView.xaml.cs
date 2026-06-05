using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StationApp.UI.Views;

public partial class ExportSummaryReportView : UserControl
{
    public ExportSummaryReportView()
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

    private static void LookupEditableTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
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
