using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class VehicleRepresentativeSelectionDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Chọn số PTVC đại diện";
    [ObservableProperty] private string _message = "Các đăng ký đang chọn có nhiều số PTVC khác nhau. Hãy chọn đăng ký đại diện để lấy Số PTVC/Mooc/Tài xế cho lượt cân.";
    [ObservableProperty] private ObservableCollection<VehicleRepresentativeOption> _options = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private VehicleRepresentativeOption? _selectedOption;

    public VehicleRepresentativeSelectionResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public VehicleRepresentativeSelectionDialogViewModel(
        IReadOnlyCollection<VehicleRepresentativeOption> options,
        Guid? preselectedRegistrationId = null)
    {
        Options = new ObservableCollection<VehicleRepresentativeOption>(options);
        SelectedOption = Options.FirstOrDefault(x => x.RegistrationId == preselectedRegistrationId)
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

        DialogResultValue = new VehicleRepresentativeSelectionResult(SelectedOption.RegistrationId);
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
    Guid RegistrationId,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    string? ErpVehicleRegistrationId,
    DateTime CreatedAt
);

public sealed record VehicleRepresentativeSelectionResult(Guid RegistrationId);
