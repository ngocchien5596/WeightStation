using System;
using System.Windows;
using System.Windows.Controls;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Views;

public partial class WeighingView : UserControl
{
    public WeighingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WeighingViewModel vm)
        {
            vm.DetachCameraPreviewHost();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
    }
}
