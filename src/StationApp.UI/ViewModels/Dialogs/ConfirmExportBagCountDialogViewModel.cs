using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class ConfirmExportBagCountDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Xác nhận số bao";
    [ObservableProperty] private string _message = "Vui lòng xác nhận số bao thực tế trước khi lưu cân lần 2.";
    [ObservableProperty] private string _sessionNo;
    [ObservableProperty] private string _vehiclePlate;
    [ObservableProperty] private string _cutOrderCode;
    [ObservableProperty] private string _weight1Text;
    [ObservableProperty] private string _weight2Text;
    [ObservableProperty] private string _netWeightText;
    [ObservableProperty] private string _bagWeightText;
    [ObservableProperty] private string _systemCalculatedBagCountText;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseCommand))]
    private string _confirmedBagCountText;

    [ObservableProperty] private bool _isReturnedBrokenTrip;
    [ObservableProperty] private string? _note;

    public int? SystemCalculatedBagCount { get; }
    public ConfirmExportBagCountDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public ConfirmExportBagCountDialogViewModel(
        string sessionNo,
        string vehiclePlate,
        string cutOrderCode,
        decimal weight1,
        decimal weight2,
        decimal netWeight,
        decimal? bagWeightKg,
        int? systemCalculatedBagCount,
        int initialConfirmedBagCount,
        bool initialIsReturnedBrokenTrip = false,
        string? initialNote = null)
    {
        SessionNo = sessionNo;
        VehiclePlate = vehiclePlate;
        CutOrderCode = cutOrderCode;
        Weight1Text = $"{weight1:N0} kg";
        Weight2Text = $"{weight2:N0} kg";
        NetWeightText = $"{netWeight:N0} kg";
        BagWeightText = bagWeightKg.HasValue && bagWeightKg.Value > 0m
            ? $"{bagWeightKg.Value:N3} kg"
            : "Chưa có TL bao";
        SystemCalculatedBagCount = systemCalculatedBagCount;
        SystemCalculatedBagCountText = systemCalculatedBagCount.HasValue
            ? $"{systemCalculatedBagCount.Value:N0}"
            : "Chưa tính được";
        ConfirmedBagCountText = Math.Max(0, initialConfirmedBagCount).ToString();
        IsReturnedBrokenTrip = initialIsReturnedBrokenTrip;
        Note = initialNote;
    }

    public bool HasManualAdjustment =>
        TryGetConfirmedBagCount(out var confirmed)
        && SystemCalculatedBagCount.HasValue
        && confirmed != SystemCalculatedBagCount.Value;

    partial void OnConfirmedBagCountTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasManualAdjustment));
    }

    private bool CanConfirm() => TryGetConfirmedBagCount(out _);

    private bool CanDecrease() => TryGetConfirmedBagCount(out var value) && value > 0;

    [RelayCommand]
    private void Increase()
    {
        var current = TryGetConfirmedBagCount(out var value) ? value : 0;
        ConfirmedBagCountText = (current + 1).ToString();
    }

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease()
    {
        if (!TryGetConfirmedBagCount(out var value) || value <= 0)
        {
            return;
        }

        ConfirmedBagCountText = (value - 1).ToString();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (!TryGetConfirmedBagCount(out var confirmedBagCount))
        {
            return;
        }

        DialogResultValue = new ConfirmExportBagCountDialogResult(
            confirmedBagCount,
            SystemCalculatedBagCount,
            IsReturnedBrokenTrip,
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private bool TryGetConfirmedBagCount(out int value)
    {
        return int.TryParse(ConfirmedBagCountText?.Trim(), out value) && value >= 0;
    }
}

public sealed record ConfirmExportBagCountDialogResult(
    int ConfirmedBagCount,
    int? SystemCalculatedBagCount,
    bool IsReturnedBrokenTrip,
    string? Note);
