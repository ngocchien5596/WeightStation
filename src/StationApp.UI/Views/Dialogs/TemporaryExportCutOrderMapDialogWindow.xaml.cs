using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class TemporaryExportCutOrderMapDialogWindow : Window
{
    public TemporaryExportCutOrderMapDialogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TemporaryExportCutOrderMapDialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is TemporaryExportCutOrderMapDialogViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        DialogResult = result;
        Close();
    }
}
