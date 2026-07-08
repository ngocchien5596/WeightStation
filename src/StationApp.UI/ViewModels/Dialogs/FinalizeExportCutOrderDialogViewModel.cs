using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class FinalizeExportCutOrderDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Ch\u1ed1t c\u1eaft l\u1ec7nh xu\u1ea5t kh\u1ea9u";
    [ObservableProperty] private string _cutOrderCode;
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _productName;
    [ObservableProperty] private string? _exportPackageTypeDisplayName;
    [ObservableProperty] private string _weighedWeightText;
    [ObservableProperty] private string _actualExportWeightText = string.Empty;
    [ObservableProperty] private string _validationMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _unweighedWeightTonsInput = "0";

    private readonly decimal _weighedWeightKg;

    public FinalizeExportCutOrderDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public FinalizeExportCutOrderDialogViewModel(
        string cutOrderCode,
        string? customerName,
        string? productName,
        string? exportPackageTypeDisplayName,
        decimal weighedWeightKg,
        decimal existingUnweighedWeightKg = 0m)
    {
        CutOrderCode = cutOrderCode;
        CustomerName = customerName;
        ProductName = productName;
        ExportPackageTypeDisplayName = exportPackageTypeDisplayName;
        _weighedWeightKg = weighedWeightKg;
        WeighedWeightText = FormatTons(weighedWeightKg);
        UnweighedWeightTonsInput = FormatInputTons(existingUnweighedWeightKg);
        RecalculatePreview();
    }

    partial void OnUnweighedWeightTonsInputChanged(string value)
    {
        RecalculatePreview();
    }

    private bool CanConfirm() => TryParseUnweighedWeightKg(out _);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (!TryParseUnweighedWeightKg(out var unweighedWeightKg))
        {
            return;
        }

        DialogResultValue = new FinalizeExportCutOrderDialogResult(unweighedWeightKg);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private void RecalculatePreview()
    {
        if (TryParseUnweighedWeightKg(out var unweighedWeightKg))
        {
            ValidationMessage = string.Empty;
            ActualExportWeightText = FormatTons(_weighedWeightKg + unweighedWeightKg);
            return;
        }

        ActualExportWeightText = FormatTons(_weighedWeightKg);
    }

    private bool TryParseUnweighedWeightKg(out decimal unweighedWeightKg)
    {
        unweighedWeightKg = 0m;
        var normalized = UnweighedWeightTonsInput?.Trim().Replace(',', '.');
        if (string.IsNullOrWhiteSpace(normalized)
            || !decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var unweighedWeightTons))
        {
            ValidationMessage = "SL kh\u00e1c kh\u00f4ng h\u1ee3p l\u1ec7.";
            return false;
        }

        if (unweighedWeightTons < 0m)
        {
            ValidationMessage = "SL kh\u00e1c ph\u1ea3i l\u1edbn h\u01a1n ho\u1eb7c b\u1eb1ng 0.";
            return false;
        }

        unweighedWeightKg = decimal.Round(unweighedWeightTons * 1000m, 3, MidpointRounding.AwayFromZero);
        return true;
    }

    private static string FormatTons(decimal weightKg)
        => $"{weightKg / 1000m:N3} t\u1ea5n";

    private static string FormatInputTons(decimal weightKg)
        => decimal.Round(weightKg / 1000m, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record FinalizeExportCutOrderDialogResult(decimal ExportUnweighedWeightKg);
