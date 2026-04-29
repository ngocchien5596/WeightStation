using System.Collections.ObjectModel;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.Printing;
using StationApp.UI.Printing;

namespace StationApp.UI.ViewModels.Dialogs;

public partial class PrintOptionsDialogViewModel : ObservableObject
{
    private readonly PrintOverlayRenderer _renderer;
    private readonly PrintTemplateDefinition _template;
    private readonly PrintBatchPreviewModel _batch;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private ObservableCollection<PrinterDescriptor> _printers = new();
    [ObservableProperty] private PrinterDescriptor? _selectedPrinter;
    [ObservableProperty] private int _copyCount = 1;
    [ObservableProperty] private double _offsetXmm;
    [ObservableProperty] private double _offsetYmm;
    [ObservableProperty] private IDocumentPaginatorSource? _previewDocument;
    [ObservableProperty] private string? _validationMessage;

    public PrintOptionsModel? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public bool CanPrint => SelectedPrinter != null && CopyCount > 0;

    public PrintOptionsDialogViewModel(
        string title,
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        IReadOnlyList<PrinterDescriptor> printers,
        PrintOverlayRenderer renderer)
    {
        Title = title;
        _template = template;
        _batch = batch;
        _renderer = renderer;
        Printers = new ObservableCollection<PrinterDescriptor>(printers);
        SelectedPrinter = Printers.FirstOrDefault(p => p.IsDefault) ?? Printers.FirstOrDefault();
        OffsetXmm = template.DefaultOffsetXmm;
        OffsetYmm = template.DefaultOffsetYmm;
        RefreshPreview();
    }

    partial void OnSelectedPrinterChanged(PrinterDescriptor? value)
    {
        ValidationMessage = value == null ? "Vui lòng chọn máy in hợp lệ." : null;
        OnPropertyChanged(nameof(CanPrint));
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnCopyCountChanged(int value)
    {
        ValidationMessage = value <= 0 ? "Số lượng bản in phải lớn hơn 0." : null;
        OnPropertyChanged(nameof(CanPrint));
        PrintCommand.NotifyCanExecuteChanged();
    }

    partial void OnOffsetXmmChanged(double value) => RefreshPreview();
    partial void OnOffsetYmmChanged(double value) => RefreshPreview();

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        DialogResultValue = new PrintOptionsModel
        {
            SelectedPrinterName = SelectedPrinter?.Name,
            CopyCount = CopyCount,
            OffsetXmm = OffsetXmm,
            OffsetYmm = OffsetYmm
        };

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private void RefreshPreview()
    {
        PreviewDocument = _renderer.CreateDocument(
            _template,
            _batch.Pages,
            new PrintOptionsModel
            {
                CopyCount = 1,
                OffsetXmm = OffsetXmm,
                OffsetYmm = OffsetYmm
            },
            previewMode: true);
    }
}
