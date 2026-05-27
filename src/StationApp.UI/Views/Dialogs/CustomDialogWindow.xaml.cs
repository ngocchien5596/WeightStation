using System.Windows;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.Views.Dialogs;

public partial class CustomDialogWindow : Window
{
    public CustomDialogWindow()
    {
        InitializeComponent();
    }

    public CustomDialogWindow(CustomDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (s, result) =>
        {
            if (result.HasValue)
            {
                DialogResult = result.Value;
            }
            else
            {
                Close();
            }
        };
    }
}
