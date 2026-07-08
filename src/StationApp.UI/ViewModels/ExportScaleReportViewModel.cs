using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Application.DTOs;
using StationApp.Application.UseCases;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class ExportScaleReportViewModel : ObservableObject
{
    private readonly BuildExportScaleSummaryReportUseCase _buildUseCase;
    private readonly ExportExportScaleSummaryReportUseCase _exportUseCase;
    private readonly GetExportScaleSummaryReportLookupOptionsUseCase _lookupOptionsUseCase;
    private readonly IToastService _toastService;
    private bool _suppressCutOrderSearchSync;

    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _cutOrderOptions = [];
    [ObservableProperty] private ICollectionView? _cutOrderOptionsView;
    [ObservableProperty] private string? _cutOrderSearchText;
    [ObservableProperty] private bool _isCutOrderDropDownOpen;
    [ObservableProperty] private ReportLookupOptionDto? _selectedCutOrder;
    [ObservableProperty] private DateTime? _targetDate = DateTime.Today;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<ExportScaleSummaryReportRow> _previewRows = [];
    [ObservableProperty] private string _previewSummaryText = "Chưa có dữ liệu xem trước.";
    [ObservableProperty] private ExportScaleSummaryReportDocument? _currentDocument;
    [ObservableProperty] private System.Windows.Documents.IDocumentPaginatorSource? _previewDocument;
    private System.Windows.Xps.Packaging.XpsDocument? _currentXpsDocument;
    private string? _currentTempXpsPath;
    private string? _currentTempExcelPath;

    private void CleanupOldPreview()
    {
        if (_currentXpsDocument != null)
        {
            Helpers.ReportPreviewHelper.CleanupPreview(_currentXpsDocument, _currentTempExcelPath, _currentTempXpsPath);
            _currentXpsDocument = null;
            _currentTempXpsPath = null;
            _currentTempExcelPath = null;
        }
        PreviewDocument = null;
    }

    ~ExportScaleReportViewModel()
    {
        CleanupOldPreview();
    }

    public ExportScaleReportViewModel(
        BuildExportScaleSummaryReportUseCase buildUseCase,
        ExportExportScaleSummaryReportUseCase exportUseCase,
        GetExportScaleSummaryReportLookupOptionsUseCase lookupOptionsUseCase,
        IToastService toastService)
    {
        _buildUseCase = buildUseCase;
        _exportUseCase = exportUseCase;
        _lookupOptionsUseCase = lookupOptionsUseCase;
        _toastService = toastService;
    }

    public async Task InitializeAsync()
    {
        var options = await _lookupOptionsUseCase.GetCutOrdersAsync(CancellationToken.None);
        CutOrderOptions = new ObservableCollection<ReportLookupOptionDto>(options);
        CutOrderOptionsView = CollectionViewSource.GetDefaultView(CutOrderOptions);
        CutOrderOptionsView.Filter = item => MatchesLookupFilter(item, CutOrderSearchText);

        _suppressCutOrderSearchSync = true;
        try
        {
            SelectedCutOrder = null;
            CutOrderSearchText = string.Empty;
            CutOrderOptionsView.Refresh();
        }
        finally
        {
            _suppressCutOrderSearchSync = false;
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var cutOrderId = ResolveSelectedCutOrderId();


        try
        {
            IsBusy = true;
            CleanupOldPreview();

            var document = await _buildUseCase.ExecuteAsync(cutOrderId, TargetDate, CancellationToken.None);
            ApplyPreview(document);

            if (document.Rows.Count == 0)
            {
                _toastService.ShowWarning("Không có dữ liệu để xem trước.");
                return;
            }

            var result = await Helpers.ReportPreviewHelper.GeneratePreviewAsync(
                "BaoCaoXuatXK",
                async (tempPath) => await _exportUseCase.ExecuteAsync(document, tempPath, CancellationToken.None)
            );

            if (result.Success && result.XpsDocument != null)
            {
                _currentXpsDocument = result.XpsDocument;
                _currentTempExcelPath = result.ExcelPath;
                _currentTempXpsPath = result.XpsPath;
                PreviewDocument = _currentXpsDocument.GetFixedDocumentSequence();
            }
            else
            {
                _toastService.ShowWarning(result.ErrorMessage ?? "Lỗi không xác định khi tạo bản xem trước.");
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể xem trước báo cáo XK: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var cutOrderId = ResolveSelectedCutOrderId();


        var cutOrderCode = ResolveSelectedCutOrderCode();
        var saveDialog = new SaveFileDialog
        {
            Title = "Xuất báo cáo xuất - XK",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = GetDefaultReportFolder(),
            FileName = $"BaoCaoXuatXK_{cutOrderCode}_{(TargetDate ?? DateTime.Today):yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var document = await _buildUseCase.ExecuteAsync(cutOrderId, TargetDate, CancellationToken.None);
            ApplyPreview(document);
            await _exportUseCase.ExecuteAsync(document, saveDialog.FileName, CancellationToken.None);
            _toastService.ShowSuccess($"Đã xuất báo cáo XK thành công:\n{saveDialog.FileName}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể xuất báo cáo XK: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnCutOrderSearchTextChanged(string? value)
    {
        CutOrderOptionsView?.Refresh();
    }

    partial void OnSelectedCutOrderChanged(ReportLookupOptionDto? value)
    {
        if (_suppressCutOrderSearchSync || value == null)
        {
            return;
        }

        if (!string.Equals(CutOrderSearchText, value.DisplayName, StringComparison.Ordinal))
        {
            CutOrderSearchText = value.DisplayName;
        }
    }

    private void ApplyPreview(ExportScaleSummaryReportDocument document)
    {
        CurrentDocument = document;
        PreviewRows = new ObservableCollection<ExportScaleSummaryReportRow>(document.Rows);
        var totalActualTon = document.Rows.Sum(x => x.ActualExportTon);
        var totalReturnedTon = document.Rows.Sum(x => x.ReturnedBrokenWeightTon);
        PreviewSummaryText = $"Số chuyến: {document.Rows.Count:N0} | Thực xuất: {totalActualTon:N3} tấn | Hồi về: {totalReturnedTon:N3} tấn";
    }

    private Guid? ResolveSelectedCutOrderId()
    {
        var selectedCutOrder = SelectedCutOrder ?? ResolveSelectedLookup(CutOrderOptions, CutOrderSearchText);
        if (selectedCutOrder == null || !Guid.TryParse(selectedCutOrder.Code, out var cutOrderId))
        {
            return null;
        }

        if (!ReferenceEquals(SelectedCutOrder, selectedCutOrder))
        {
            SelectedCutOrder = selectedCutOrder;
        }

        return cutOrderId;
    }
    private string ResolveSelectedCutOrderCode()
    {
        var selectedCutOrder = SelectedCutOrder ?? ResolveSelectedLookup(CutOrderOptions, CutOrderSearchText);
        if (selectedCutOrder == null || string.IsNullOrWhiteSpace(selectedCutOrder.DisplayName))
        {
            return "TatCa";
        }

        var raw = selectedCutOrder.DisplayName.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(raw) ? "TatCa" : raw.Replace('/', '-');
    }

    private static bool MatchesLookupFilter(object item, string? keyword)
    {
        if (item is not ReportLookupOptionDto option)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return option.DisplayName.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static ReportLookupOptionDto? ResolveSelectedLookup(
        IEnumerable<ReportLookupOptionDto> options,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var normalized = keyword.Trim();
        return options.FirstOrDefault(x =>
            string.Equals(x.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDefaultReportFolder()
    {
        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        return Directory.Exists(downloadsFolder)
            ? downloadsFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
