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

public partial class InboundSummaryReportViewModel : ObservableObject
{
    private readonly BuildInboundSummaryReportUseCase _buildUseCase;
    private readonly ExportInboundSummaryReportUseCase _exportUseCase;
    private readonly GetInboundSummaryReportLookupOptionsUseCase _lookupOptionsUseCase;
    private readonly IClock _clock;
    private readonly IToastService _toastService;
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
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _productOptions = [];
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _customerOptions = [];
    [ObservableProperty] private ICollectionView? _productOptionsView;
    [ObservableProperty] private ICollectionView? _customerOptionsView;
    [ObservableProperty] private string? _productSearchText;
    [ObservableProperty] private string? _customerSearchText;
    [ObservableProperty] private bool _isProductDropDownOpen;
    [ObservableProperty] private bool _isCustomerDropDownOpen;
    [ObservableProperty] private ReportLookupOptionDto? _selectedProduct;
    [ObservableProperty] private ReportLookupOptionDto? _selectedCustomer;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<InboundSummaryReportRow> _previewRows = [];
    [ObservableProperty] private string _previewSummaryText = "Chưa có dữ liệu xem trước.";
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

    ~InboundSummaryReportViewModel()
    {
        CleanupOldPreview();
    }

    public InboundSummaryReportViewModel(
        BuildInboundSummaryReportUseCase buildUseCase,
        ExportInboundSummaryReportUseCase exportUseCase,
        GetInboundSummaryReportLookupOptionsUseCase lookupOptionsUseCase,
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

        var productOptions = await _lookupOptionsUseCase.GetProductsAsync(CancellationToken.None);
        var customerOptions = await _lookupOptionsUseCase.GetCustomersAsync(CancellationToken.None);

        ProductOptions = new ObservableCollection<ReportLookupOptionDto>(
            [new ReportLookupOptionDto(string.Empty, "-- Tất cả sản phẩm --"), .. productOptions]);
        CustomerOptions = new ObservableCollection<ReportLookupOptionDto>(
            [new ReportLookupOptionDto(string.Empty, "-- Tất cả khách hàng --"), .. customerOptions]);

        ProductOptionsView = CollectionViewSource.GetDefaultView(ProductOptions);
        ProductOptionsView.Filter = item => MatchesLookupFilter(item, ProductSearchText);
        CustomerOptionsView = CollectionViewSource.GetDefaultView(CustomerOptions);
        CustomerOptionsView.Filter = item => MatchesLookupFilter(item, CustomerSearchText);

        SelectedProduct = null;
        SelectedCustomer = null;
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryBuildDateRange(out _, out _, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        try
        {
            IsBusy = true;
            CleanupOldPreview();

            var document = await BuildDocumentFromCurrentFilterAsync();
            ApplyPreview(document);
            
            if (document.Rows.Count == 0)
            {
                _toastService.ShowWarning("Không có dữ liệu để xem trước.");
                return;
            }

            var result = await Helpers.ReportPreviewHelper.GeneratePreviewAsync(
                "BaoCaoCanHang",
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
            _toastService.ShowError($"Không thể xem trước báo cáo: {ex.Message}");
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

        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Xuất báo cáo cân hàng",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = GetDefaultReportFolder(),
            FileName = $"BaoCaoCanHang_{fromTime:yyyyMMdd_HHmmss}_{toTime:yyyyMMdd_HHmmss}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var document = await BuildDocumentFromCurrentFilterAsync();
            ApplyPreview(document);
            await _exportUseCase.ExecuteAsync(document, saveDialog.FileName, CancellationToken.None);

            _toastService.ShowSuccess($"Đã xuất báo cáo thành công:\n{saveDialog.FileName}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể xuất báo cáo: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<InboundSummaryReportDocument> BuildDocumentFromCurrentFilterAsync()
    {
        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var selectedProduct = SelectedProduct ?? ResolveSelectedLookup(ProductOptions, ProductSearchText);
        var selectedCustomer = SelectedCustomer ?? ResolveSelectedLookup(CustomerOptions, CustomerSearchText);
        var filter = new InboundSummaryReportFilter(
            fromTime,
            toTime,
            NormalizeCode(selectedProduct),
            NormalizeCode(selectedCustomer));

        return await _buildUseCase.ExecuteAsync(filter, CancellationToken.None);
    }

    private void ApplyPreview(InboundSummaryReportDocument document)
    {
        PreviewRows = new ObservableCollection<InboundSummaryReportRow>(document.Rows);
        PreviewSummaryText = $"Số dòng: {document.Rows.Count:N0} | Tổng KL hàng: {document.TotalNetWeightKg:N0} kg";
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

    private static string GetDefaultReportFolder()
    {
        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        return Directory.Exists(downloadsFolder)
            ? downloadsFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            errorMessage = "Vui lòng chọn ngày cho Từ giờ.";
            return false;
        }

        if (!ToDate.HasValue)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Vui lòng chọn ngày cho Đến giờ.";
            return false;
        }

        if (!int.TryParse(FromHour, out var fromHour) || fromHour is < 0 or > 23)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Giờ của Từ giờ không hợp lệ.";
            return false;
        }

        if (!int.TryParse(FromMinute, out var fromMinute) || fromMinute is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Phút của Từ giờ không hợp lệ.";
            return false;
        }

        if (!int.TryParse(FromSecond, out var fromSecond) || fromSecond is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Giay cua Tu gio khong hop le.";
            return false;
        }

        if (!int.TryParse(ToHour, out var toHour) || toHour is < 0 or > 23)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Giờ của Đến giờ không hợp lệ.";
            return false;
        }

        if (!int.TryParse(ToMinute, out var toMinute) || toMinute is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Phút của Đến giờ không hợp lệ.";
            return false;
        }

        if (!int.TryParse(ToSecond, out var toSecond) || toSecond is < 0 or > 59)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Giay cua Den gio khong hop le.";
            return false;
        }

        fromTime = FromDate.Value.Date.AddHours(fromHour).AddMinutes(fromMinute).AddSeconds(fromSecond);
        toTime = ToDate.Value.Date.AddHours(toHour).AddMinutes(toMinute).AddSeconds(toSecond);

        if (fromTime > toTime)
        {
            errorMessage = "Từ giờ không được lớn hơn Đến giờ.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    partial void OnProductSearchTextChanged(string? value)
    {
        ProductOptionsView?.Refresh();
    }

    partial void OnCustomerSearchTextChanged(string? value)
    {
        CustomerOptionsView?.Refresh();
    }

    partial void OnSelectedProductChanged(ReportLookupOptionDto? value)
    {
        if (value != null)
        {
            var displayText = string.IsNullOrWhiteSpace(value.Code) ? string.Empty : value.DisplayName;
            if (string.Equals(ProductSearchText, displayText, StringComparison.Ordinal))
            {
                return;
            }

            ProductSearchText = displayText;
        }
    }

    partial void OnSelectedCustomerChanged(ReportLookupOptionDto? value)
    {
        if (value != null)
        {
            var displayText = string.IsNullOrWhiteSpace(value.Code) ? string.Empty : value.DisplayName;
            if (string.Equals(CustomerSearchText, displayText, StringComparison.Ordinal))
            {
                return;
            }

            CustomerSearchText = displayText;
        }
    }

    private static string? NormalizeCode(ReportLookupOptionDto? option)
        => option == null || string.IsNullOrWhiteSpace(option.Code) ? null : option.Code.Trim();

    private static bool MatchesLookupFilter(object item, string? keyword)
    {
        if (item is not ReportLookupOptionDto option)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(option.Code) || string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var normalized = keyword.Trim();
        return option.Code.Contains(normalized, StringComparison.OrdinalIgnoreCase)
               || option.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase);
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
            string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.DisplayName, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
