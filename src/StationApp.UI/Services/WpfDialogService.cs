using System;
using System.Threading.Tasks;
using System.Windows;
using StationApp.UI.ViewModels.Dialogs;
using StationApp.UI.Views.Dialogs;

namespace StationApp.UI.Services;

public class WpfDialogService : IDialogService
{
    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Đồng ý", string cancelText = "Hủy")
    {
        var tcs = new TaskCompletionSource<bool>();
        
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = new CustomDialogViewModel(title, message, DialogType.Confirm, confirmText, cancelText);
            var win = new CustomDialogWindow(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            
            var result = win.ShowDialog();
            tcs.SetResult(result == true);
        });

        return tcs.Task;
    }

    public Task ShowWarningAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = new CustomDialogViewModel(title, message, DialogType.Warning, "OK", string.Empty);
            var win = new CustomDialogWindow(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            win.ShowDialog();
            tcs.SetResult();
        });

        return tcs.Task;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = new CustomDialogViewModel(title, message, DialogType.Error, "Đóng", string.Empty);
            var win = new CustomDialogWindow(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            win.ShowDialog();
            tcs.SetResult();
        });

        return tcs.Task;
    }

    public Task ShowInfoAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = new CustomDialogViewModel(title, message, DialogType.Info, "Đóng", string.Empty);
            var win = new CustomDialogWindow(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            win.ShowDialog();
            tcs.SetResult();
        });

        return tcs.Task;
    }

    public Task<TResult?> ShowCustomDialogAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : class
    {
        // Flexible fallback if needed for future advanced modals
        var tcs = new TaskCompletionSource<TResult?>();
        
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // By convention or mapping: TViewModel -> TView
            var vmName = viewModel.GetType().Name;
            var viewName = vmName.Replace("ViewModel", "Window");
            var viewType = Type.GetType($"StationApp.UI.Views.Dialogs.{viewName}") 
                           ?? Type.GetType($"StationApp.UI.Views.{viewName}");

            if (viewType != null)
            {
                var win = Activator.CreateInstance(viewType) as Window;
                if (win != null)
                {
                    win.DataContext = viewModel;
                    win.Owner = System.Windows.Application.Current.MainWindow;
                    
                    var result = win.ShowDialog();
                    
                    // Try getting a Result property from ViewModel if exists
                    var resultProp = viewModel.GetType().GetProperty("DialogResultValue");
                    if (resultProp != null)
                    {
                        var val = (TResult?)resultProp.GetValue(viewModel);
                        tcs.SetResult(val);
                        return;
                    }
                }
            }
            
            tcs.SetResult(default);
        });

        return tcs.Task;
    }
}
