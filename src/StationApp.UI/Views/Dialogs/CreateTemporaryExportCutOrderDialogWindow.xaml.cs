using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class CreateTemporaryExportCutOrderDialogWindow : Window
{
    public CreateTemporaryExportCutOrderDialogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CreateTemporaryExportCutOrderDialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is CreateTemporaryExportCutOrderDialogViewModel newVm)
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
