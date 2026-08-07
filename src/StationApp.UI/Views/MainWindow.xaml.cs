using System.Windows;
using System.Windows.Input;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewMouseWheel += OnPreviewMouseWheel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.Delta > 0)
                {
                    if (vm.ZoomInCommand.CanExecute(null))
                    {
                        vm.ZoomInCommand.Execute(null);
                    }
                }
                else if (e.Delta < 0)
                {
                    if (vm.ZoomOutCommand.CanExecute(null))
                    {
                        vm.ZoomOutCommand.Execute(null);
                    }
                }
                e.Handled = true;
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DataContext is MainViewModel vm)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    if (vm.ZoomInCommand.CanExecute(null))
                    {
                        vm.ZoomInCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    if (vm.ZoomOutCommand.CanExecute(null))
                    {
                        vm.ZoomOutCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    if (vm.ResetZoomCommand.CanExecute(null))
                    {
                        vm.ResetZoomCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
