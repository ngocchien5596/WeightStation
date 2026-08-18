using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class ExportScaleReportViewModel : ObservableObject
{
    private readonly BuildExportScaleSummaryReportUseCase _buildUseCase;
    private readonly ExportExportScaleSummaryReportUseCase _exportUseCase;
    private readonly GetExportScaleSummaryReportLookupOptionsUseCase _lookupOptionsUseCase;
    private readonly IClock _clock;
    private readonly IToastService _toastService;
    private bool _suppressCutOrderSearchSync;
    private bool _suppressTimeChanged;

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private string? _fromHour;
    [ObservableProperty] private string? _fromMinute;
    [ObservableProperty] private string? _fromSecond;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _toHour;
    [ObservableProperty] private string? _toMinute;
    [ObservableProperty] private string? _toSecond;
    [ObservableProperty] private ObservableCollection<string> _hourOptions = [];
    [ObservableProperty] private ObservableCollection<string> _minuteOptions = [];
    [ObservableProperty] private ObservableCollection<string> _secondOptions = [];
    [ObservableProperty] private ObservableCollection<ShiftReportShiftOption> _shiftOptions = [];
    [ObservableProperty] private ShiftReportShiftOption? _selectedShift;
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
        IClock clock,
        IToastService toastService)
    {
        _buildUseCase = buildUseCase;
        _exportUseCase = exportUseCase;
        _lookupOptionsUseCase = lookupOptionsUseCase;
        _clock = clock;
        _toastService = toastService;
    }

    public async Task InitializeAsync()
    {
        HourOptions = new ObservableCollection<string>(Enumerable.Range(0, 24).Select(x => x.ToString("00")));
        MinuteOptions = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(x => x.ToString("00")));
        SecondOptions = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(x => x.ToString("00")));
        ShiftOptions =
        [
            new("CA1", "Ca 1"),
            new("CA2", "Ca 2"),
            new("CA3", "Ca 3"),
            new("CUSTOM", "Tùy chỉnh")
        ];
        ApplyCurrentShift();

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

        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        try
        {
            IsBusy = true;
            CleanupOldPreview();

            var document = await _buildUseCase.ExecuteAsync(cutOrderId, fromTime, toTime, TargetDate, CancellationToken.None);
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

        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
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
            FileName = $"BaoCaoXuatXK_{cutOrderCode}_{fromTime:yyyyMMdd_HHmmss}_{toTime:yyyyMMdd_HHmmss}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var document = await _buildUseCase.ExecuteAsync(cutOrderId, fromTime, toTime, TargetDate, CancellationToken.None);
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

    partial void OnSelectedShiftChanged(ShiftReportShiftOption? value)
    {
        if (value == null || string.Equals(value.Code, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyShift(FromDate ?? _clock.NowLocal.Date, value.Code);
    }

    partial void OnFromDateChanged(DateTime? value) => MarkCustomShift();
    partial void OnFromHourChanged(string? value) => MarkCustomShift();
    partial void OnFromMinuteChanged(string? value) => MarkCustomShift();
    partial void OnFromSecondChanged(string? value) => MarkCustomShift();
    partial void OnToDateChanged(DateTime? value) => MarkCustomShift();
    partial void OnToHourChanged(string? value) => MarkCustomShift();
    partial void OnToMinuteChanged(string? value) => MarkCustomShift();
    partial void OnToSecondChanged(string? value) => MarkCustomShift();

    private void ApplyCurrentShift()
    {
        var now = _clock.NowLocal;
        var (fromTime, toTime) = ResolveShiftRange(now);
        _suppressTimeChanged = true;
        try
        {
            SelectedShift = ResolveCurrentShift(now);
            SetDateTimeRange(fromTime, toTime);
            TargetDate = now.Date;
        }
        finally
        {
            _suppressTimeChanged = false;
        }
    }

    private void ApplyShift(DateTime reportDate, string shiftCode)
    {
        var (fromTime, toTime) = ResolveShiftRange(reportDate.Date, shiftCode);
        _suppressTimeChanged = true;
        try
        {
            SetDateTimeRange(fromTime, toTime);
        }
        finally
        {
            _suppressTimeChanged = false;
        }
    }

    private void SetDateTimeRange(DateTime fromTime, DateTime toTime)
    {
        FromDate = fromTime.Date;
        FromHour = fromTime.Hour.ToString("00");
        FromMinute = fromTime.Minute.ToString("00");
        FromSecond = fromTime.Second.ToString("00");
        ToDate = toTime.Date;
        ToHour = toTime.Hour.ToString("00");
        ToMinute = toTime.Minute.ToString("00");
        ToSecond = toTime.Second.ToString("00");
    }

    private void MarkCustomShift()
    {
        if (_suppressTimeChanged || ShiftOptions.Count == 0)
        {
            return;
        }

        var custom = ShiftOptions.FirstOrDefault(x => string.Equals(x.Code, "CUSTOM", StringComparison.OrdinalIgnoreCase));
        if (custom != null && !ReferenceEquals(SelectedShift, custom))
        {
            SelectedShift = custom;
        }
    }

    private static (DateTime FromTime, DateTime ToTime) ResolveShiftRange(DateTime now)
    {
        var today = now.Date;
        var timeOfDay = now.TimeOfDay;

        if (timeOfDay >= TimeSpan.FromHours(6) && timeOfDay < TimeSpan.FromHours(14))
        {
            return (today.AddHours(6), today.AddHours(14).AddSeconds(-1));
        }

        if (timeOfDay >= TimeSpan.FromHours(14) && timeOfDay < TimeSpan.FromHours(22))
        {
            return (today.AddHours(14), today.AddHours(22).AddSeconds(-1));
        }

        if (timeOfDay >= TimeSpan.FromHours(22))
        {
            return (today.AddHours(22), today.AddDays(1).AddHours(6).AddSeconds(-1));
        }

        return (today.AddDays(-1).AddHours(22), today.AddHours(6).AddSeconds(-1));
    }

    private ShiftReportShiftOption ResolveCurrentShift(DateTime now)
    {
        var code = now.TimeOfDay switch
        {
            var time when time >= TimeSpan.FromHours(6) && time < TimeSpan.FromHours(14) => "CA1",
            var time when time >= TimeSpan.FromHours(14) && time < TimeSpan.FromHours(22) => "CA2",
            _ => "CA3"
        };

        return ShiftOptions.First(x => x.Code == code);
    }

    private static (DateTime FromTime, DateTime ToTime) ResolveShiftRange(DateTime reportDate, string shiftCode)
        => shiftCode switch
        {
            "CA1" => (reportDate.AddHours(6), reportDate.AddHours(14).AddSeconds(-1)),
            "CA2" => (reportDate.AddHours(14), reportDate.AddHours(22).AddSeconds(-1)),
            "CA3" => (reportDate.AddHours(22), reportDate.AddDays(1).AddHours(6).AddSeconds(-1)),
            _ => (reportDate.AddHours(6), reportDate.AddHours(14).AddSeconds(-1))
        };

    private bool TryBuildDateRange(out DateTime fromTime, out DateTime toTime, out string errorMessage)
    {
        if (!FromDate.HasValue)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Vui l\u00F2ng ch\u1ECDn ng\u00E0y cho T\u1EEB gi\u1EDD.";
            return false;
        }

        if (!ToDate.HasValue)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Vui l\u00F2ng ch\u1ECDn ng\u00E0y cho \u0110\u1EBFn gi\u1EDD.";
            return false;
        }

        if (!int.TryParse(FromHour, out var fromHour) || fromHour is < 0 or > 23)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Gi\u1EDD c\u1EE7a T\u1EEB gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        if (!int.TryParse(FromMinute, out var fromMinute) || fromMinute is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Ph\u00FAt c\u1EE7a T\u1EEB gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        if (!int.TryParse(FromSecond, out var fromSecond) || fromSecond is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Gi\u00E2y c\u1EE7a T\u1EEB gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        if (!int.TryParse(ToHour, out var toHour) || toHour is < 0 or > 23)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Gi\u1EDD c\u1EE7a \u0110\u1EBFn gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        if (!int.TryParse(ToMinute, out var toMinute) || toMinute is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Ph\u00FAt c\u1EE7a \u0110\u1EBFn gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        if (!int.TryParse(ToSecond, out var toSecond) || toSecond is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Gi\u00E2y c\u1EE7a \u0110\u1EBFn gi\u1EDD kh\u00F4ng h\u1EE3p l\u1EC7.";
            return false;
        }

        fromTime = FromDate.Value.Date.AddHours(fromHour).AddMinutes(fromMinute).AddSeconds(fromSecond);
        toTime = ToDate.Value.Date.AddHours(toHour).AddMinutes(toMinute).AddSeconds(toSecond);

        if (fromTime > toTime)
        {
            errorMessage = "T\u1EEB gi\u1EDD kh\u00F4ng \u0111\u01B0\u1EE3c l\u1EDBn h\u01A1n \u0110\u1EBFn gi\u1EDD.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
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
