using System.Windows.Controls;
using StationApp.UI.ViewModels.Settings;

namespace StationApp.UI.Views.Settings
{
    public partial class SystemSettingsView : UserControl
    {
        public SystemSettingsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SystemSettingsViewModel vm && CentralApiKeyBox.Password != vm.CentralApiKey)
            {
                CentralApiKeyBox.Password = vm.CentralApiKey;
            }
        }

        private void CentralApiKeyBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SystemSettingsViewModel vm && sender is PasswordBox passwordBox)
            {
                vm.CentralApiKey = passwordBox.Password;
            }
        }
    }
}
