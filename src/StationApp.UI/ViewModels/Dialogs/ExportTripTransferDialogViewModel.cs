using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class ExportTripTransferDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Chuyển chuyến xe sang cắt lệnh khác";
    [ObservableProperty] private string _message = "Chọn cắt lệnh đích để chuyển chuyến xe đã cân nhầm.";
    [ObservableProperty] private ObservableCollection<ExportTripTransferOption> _options = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private ExportTripTransferOption? _selectedOption;

    public ExportTripTransferDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public ExportTripTransferDialogViewModel(
        IReadOnlyCollection<ExportTripTransferOption> options,
        Guid? preselectedCutOrderId = null)
    {
        Options = new ObservableCollection<ExportTripTransferOption>(options);
        SelectedOption = Options.FirstOrDefault(x => x.CutOrderId == preselectedCutOrderId)
            ?? Options.FirstOrDefault();
    }

    private bool CanConfirm() => SelectedOption != null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedOption == null)
        {
            return;
        }

        DialogResultValue = new ExportTripTransferDialogResult(SelectedOption.CutOrderId);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }
}

public sealed record ExportTripTransferOption(
    Guid CutOrderId,
    string? ErpCutOrderId,
    string VehiclePlate,
    string? CustomerName,
    string? ProductName,
    decimal? PlannedWeight,
    decimal RemainingWeight,
    int TripCount,
    DateTime? LastTripAt);

public sealed record ExportTripTransferDialogResult(Guid CutOrderId);
