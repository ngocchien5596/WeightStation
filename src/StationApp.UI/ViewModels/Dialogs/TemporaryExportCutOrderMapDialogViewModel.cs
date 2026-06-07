using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.DTOs;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class TemporaryExportCutOrderMapDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Map cắt lệnh xuất khẩu tạm";
    [ObservableProperty] private string _message = "Có cắt lệnh xuất khẩu tạm đang hoạt động. Chọn cắt lệnh tạm để map chuyến xe sang cắt lệnh thật, hoặc bỏ qua để chuyển như luồng cũ.";
    [ObservableProperty] private ObservableCollection<TemporaryExportCutOrderOption> _options = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private TemporaryExportCutOrderOption? _selectedOption;

    public TemporaryExportCutOrderMapDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public TemporaryExportCutOrderMapDialogViewModel(IReadOnlyCollection<TemporaryExportCutOrderOption> options)
    {
        Options = new ObservableCollection<TemporaryExportCutOrderOption>(options);
        SelectedOption = Options.FirstOrDefault();
    }

    private bool CanConfirm() => SelectedOption != null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedOption == null)
        {
            return;
        }

        DialogResultValue = new TemporaryExportCutOrderMapDialogResult(SelectedOption.CutOrderId, false);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void SkipMapping()
    {
        DialogResultValue = new TemporaryExportCutOrderMapDialogResult(null, true);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }
}

public sealed record TemporaryExportCutOrderMapDialogResult(Guid? TemporaryCutOrderId, bool SkipMapping);
