using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class OverToleranceWarningDialogWindow : Window
{
    public OverToleranceWarningDialogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OverToleranceWarningDialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is OverToleranceWarningDialogViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool? result)
    {
        DialogResult = result == true;
        Close();
    }
}
