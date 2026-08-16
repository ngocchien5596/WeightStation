using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public enum OverToleranceWarningDialogResult
{
    Save,
    Cancel
}

public partial class OverToleranceWarningDialogViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<bool>> _printInspectionReportAsync;

    [ObservableProperty] private string _title = "C\u1ea3nh b\u00e1o v\u01b0\u1ee3t dung sai";
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isPrinting;
    [ObservableProperty] private string? _printStatusMessage;

    public OverToleranceWarningDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool?>? CloseRequested;

    public OverToleranceWarningDialogViewModel(
        string message,
        Func<CancellationToken, Task<bool>> printInspectionReportAsync)
    {
        Message = message;
        _printInspectionReportAsync = printInspectionReportAsync;
    }

    public bool CanPrintInspectionReport => !IsPrinting;

    partial void OnIsPrintingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPrintInspectionReport));
        PrintInspectionReportCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrintInspectionReport))]
    private async Task PrintInspectionReportAsync()
    {
        IsPrinting = true;
        PrintStatusMessage = null;
        try
        {
            var printed = await _printInspectionReportAsync(CancellationToken.None);
            PrintStatusMessage = printed
                ? "\u0110\u00e3 g\u1eedi l\u1ec7nh in bi\u00ean b\u1ea3n."
                : "\u0110\u00e3 h\u1ee7y in bi\u00ean b\u1ea3n.";
        }
        catch (Exception ex)
        {
            PrintStatusMessage = $"Kh\u00f4ng th\u1ec3 in bi\u00ean b\u1ea3n: {ex.Message}";
        }
        finally
        {
            IsPrinting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Save()
    {
        DialogResultValue = OverToleranceWarningDialogResult.Save;
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Cancel()
    {
        DialogResultValue = OverToleranceWarningDialogResult.Cancel;
        CloseRequested?.Invoke(this, false);
    }

    [RelayCommand]
    private void Close()
    {
        DialogResultValue = OverToleranceWarningDialogResult.Cancel;
        CloseRequested?.Invoke(this, false);
    }

    private bool CanClose() => !IsPrinting;
}
