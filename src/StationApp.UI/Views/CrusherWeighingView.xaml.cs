using System.Windows.Controls;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Views;

public partial class CrusherWeighingView : UserControl
{
    public CrusherWeighingView()
    {
        InitializeComponent();
    }

    private async void SessionSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is CrusherWeighingViewModel vm)
        {
            await vm.LoadSessionsCommand.ExecuteAsync(null);
        }
    }
}
