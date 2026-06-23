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

        if (!TryResolveSelectedCutOrderId(out var cutOrderId, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        try
        {
            IsBusy = true;
            var document = await _buildUseCase.ExecuteAsync(cutOrderId, TargetDate, CancellationToken.None);
            ApplyPreview(document);
            _toastService.ShowSuccess($"Đã tải xem trước {document.Rows.Count:N0} chuyến xe.");
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

        if (!TryResolveSelectedCutOrderId(out var cutOrderId, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

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

    private bool TryResolveSelectedCutOrderId(out Guid cutOrderId, out string errorMessage)
    {
        var selectedCutOrder = SelectedCutOrder ?? ResolveSelectedLookup(CutOrderOptions, CutOrderSearchText);
        if (selectedCutOrder == null || !Guid.TryParse(selectedCutOrder.Code, out cutOrderId))
        {
            cutOrderId = Guid.Empty;
            errorMessage = "Vui lòng chọn 1 cắt lệnh xuất khẩu.";
            return false;
        }

        if (!ReferenceEquals(SelectedCutOrder, selectedCutOrder))
        {
            SelectedCutOrder = selectedCutOrder;
        }

        errorMessage = string.Empty;
        return true;
    }

    private string ResolveSelectedCutOrderCode()
    {
        var selectedCutOrder = SelectedCutOrder ?? ResolveSelectedLookup(CutOrderOptions, CutOrderSearchText);
        if (selectedCutOrder == null || string.IsNullOrWhiteSpace(selectedCutOrder.DisplayName))
        {
            return "CutOrder";
        }

        var raw = selectedCutOrder.DisplayName.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(raw) ? "CutOrder" : raw.Replace('/', '-');
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
