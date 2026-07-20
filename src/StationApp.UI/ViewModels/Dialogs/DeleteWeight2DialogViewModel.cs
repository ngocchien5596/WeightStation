using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class DeleteWeight2DialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "X\u00f3a l\u01b0\u1ee3t c\u00e2n l\u1ea7n 2";
    [ObservableProperty] private string _sessionNo = string.Empty;
    [ObservableProperty] private string _vehiclePlate = string.Empty;
    [ObservableProperty] private string? _cutOrderCode;
    [ObservableProperty] private string? _weight1Text;
    [ObservableProperty] private string? _weight2Text;
    [ObservableProperty] private string? _netWeightText;
    [ObservableProperty] private string? _printedDocumentWarning;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _reason;

    public DeleteWeight2DialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public DeleteWeight2DialogViewModel(
        string sessionNo,
        string vehiclePlate,
        string? cutOrderCode,
        string? weight1Text,
        string? weight2Text,
        string? netWeightText,
        bool hasPrintedWeighTicket,
        bool hasPrintedDeliveryTicket)
    {
        SessionNo = sessionNo;
        VehiclePlate = vehiclePlate;
        CutOrderCode = cutOrderCode;
        Weight1Text = weight1Text;
        Weight2Text = weight2Text;
        NetWeightText = netWeightText;

        if (hasPrintedWeighTicket || hasPrintedDeliveryTicket)
        {
            PrintedDocumentWarning = "L\u01b0\u1ee3t c\u00e2n n\u00e0y \u0111\u00e3 ph\u00e1t sinh phi\u1ebfu in. Khi x\u00f3a c\u00e2n l\u1ea7n 2, phi\u1ebfu li\u00ean quan s\u1ebd \u0111\u01b0\u1ee3c h\u1ee7y d\u1eef li\u1ec7u v\u00e0 c\u1ea7n in l\u1ea1i sau khi c\u00e2n l\u1ea1i.";
        }
    }

    private bool CanConfirm() => !string.IsNullOrWhiteSpace(Reason);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            return;
        }

        DialogResultValue = new DeleteWeight2DialogResult(Reason.Trim());
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }
}

public sealed record DeleteWeight2DialogResult(string Reason);
