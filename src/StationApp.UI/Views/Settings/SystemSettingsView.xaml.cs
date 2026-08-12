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

            if (DataContext is SystemSettingsViewModel vm2 && BackupSyncApiKeyBox.Password != vm2.BackupSyncApiKey)
            {
                BackupSyncApiKeyBox.Password = vm2.BackupSyncApiKey;
            }
        }

        private void CentralApiKeyBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SystemSettingsViewModel vm && sender is PasswordBox passwordBox)
            {
                vm.CentralApiKey = passwordBox.Password;
            }
        }

        private void BackupSyncApiKeyBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SystemSettingsViewModel vm && sender is PasswordBox passwordBox)
            {
                vm.BackupSyncApiKey = passwordBox.Password;
            }
        }
    }
}
