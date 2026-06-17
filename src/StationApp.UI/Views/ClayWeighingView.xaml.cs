using System.Windows.Controls;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Views;

public partial class ClayWeighingView : UserControl
{
    public ClayWeighingView()
    {
        InitializeComponent();
    }

    private async void SessionSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is ClayWeighingViewModel vm)
        {
            await vm.LoadSessionsCommand.ExecuteAsync(null);
        }
    }
}
