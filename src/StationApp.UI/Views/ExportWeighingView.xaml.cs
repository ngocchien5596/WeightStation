using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StationApp.Application.DTOs;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Views;

public partial class ExportWeighingView : UserControl
{
    private ExportWeighingViewModel? _viewModel;
    private bool _isClearingTripSelection;
    private int _pendingTripSelectionClears;
    private int _tripSelectionResetVersion;

    public ExportWeighingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as ExportWeighingViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExportWeighingViewModel.ClearTripSelectionRequest))
        {
            return;
        }

        var resetVersion = ++_tripSelectionResetVersion;
        _viewModel?.BeginTripSelectionReset();
        QueueTripSelectionClear(resetVersion, DispatcherPriority.Send);
        QueueTripSelectionClear(resetVersion, DispatcherPriority.Loaded);
        QueueTripSelectionClear(resetVersion, DispatcherPriority.ContextIdle);
        QueueTripSelectionClear(resetVersion, DispatcherPriority.ApplicationIdle);
    }

    private void OnTripsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LogTripGridSelectionState("SelectionChanged");
    }

    private void OnTripsGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isClearingTripSelection || _pendingTripSelectionClears > 0)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not { Item: ExportVehicleTripListItem trip } row)
        {
            return;
        }

        TripsDataGrid.SelectedItem = trip;
        TripsDataGrid.CurrentItem = trip;
        row.IsSelected = true;

        if (_viewModel != null && !ReferenceEquals(_viewModel.SelectedTrip, trip))
        {
            _viewModel.SelectedTrip = trip;
        }

        LogTripGridSelectionState("PreviewMouseLeftButtonDown/AppliedRow");
    }

    private void QueueTripSelectionClear(int resetVersion, DispatcherPriority priority)
    {
        _pendingTripSelectionClears++;
        Dispatcher.BeginInvoke(
            () =>
            {
                try
                {
                    if (resetVersion == _tripSelectionResetVersion)
                    {
                        ClearTripsGridSelection($"ClearRequest/{priority}");
                    }
                }
                finally
                {
                    _pendingTripSelectionClears = Math.Max(0, _pendingTripSelectionClears - 1);
                    if (_pendingTripSelectionClears == 0 && resetVersion == _tripSelectionResetVersion)
                    {
                        _viewModel?.CompleteTripSelectionReset();
                    }
                }
            },
            priority);
    }

    private void ClearTripsGridSelection(string source)
    {
        _isClearingTripSelection = true;
        try
        {
            TripsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TripsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            TripsDataGrid.SelectedItem = null;
            TripsDataGrid.SelectedIndex = -1;
            TripsDataGrid.CurrentCell = default;
            TripsDataGrid.CurrentItem = null;
            TripsDataGrid.UnselectAll();

            if (CollectionViewSource.GetDefaultView(TripsDataGrid.ItemsSource) is { } view)
            {
                view.MoveCurrentToPosition(-1);
            }

            LogTripGridSelectionState(source, true);
        }
        finally
        {
            _isClearingTripSelection = false;
        }
    }

    private void LogTripGridSelectionState(string source, bool isResettingSelection = false)
    {
        _viewModel?.LogTripGridSelectionState(
            source,
            TripsDataGrid.SelectedItem as ExportVehicleTripListItem,
            TripsDataGrid.SelectedIndex,
            TripsDataGrid.CurrentItem as ExportVehicleTripListItem,
            isResettingSelection || _pendingTripSelectionClears > 0 || _isClearingTripSelection);
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
