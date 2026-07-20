using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class DeleteWeight2DialogWindow : Window
{
    public DeleteWeight2DialogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DeleteWeight2DialogViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is DeleteWeight2DialogViewModel newVm)
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
