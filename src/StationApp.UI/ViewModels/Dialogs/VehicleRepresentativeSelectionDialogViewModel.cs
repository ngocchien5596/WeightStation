using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class VehicleRepresentativeSelectionDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Chọn số PTVC đại diện";
    [ObservableProperty] private string _message = "Các cắt lệnh đang chọn có nhiều số PTVC khác nhau. Hãy chọn cắt lệnh đại diện để lấy Số PTVC/Mooc/Tài xế cho lượt cân.";
    [ObservableProperty] private ObservableCollection<VehicleRepresentativeOption> _options = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private VehicleRepresentativeOption? _selectedOption;

    public VehicleRepresentativeSelectionResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public VehicleRepresentativeSelectionDialogViewModel(
        IReadOnlyCollection<VehicleRepresentativeOption> options,
        Guid? preselectedCutOrderId = null)
    {
        Options = new ObservableCollection<VehicleRepresentativeOption>(options);
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

        DialogResultValue = new VehicleRepresentativeSelectionResult(SelectedOption.CutOrderId);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }
}

public sealed record VehicleRepresentativeOption(
    Guid CutOrderId,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    string? ErpCutOrderId,
    DateTime CreatedAt
);

public sealed record VehicleRepresentativeSelectionResult(Guid CutOrderId);


