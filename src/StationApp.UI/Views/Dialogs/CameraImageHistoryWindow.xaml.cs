using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class CameraImageHistoryWindow : Window
{
    public CameraImageHistoryWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CameraImageHistoryViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is CameraImageHistoryViewModel newVm)
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
