using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public sealed record ShiftReportShiftOption(string Code, string DisplayName);

public partial class ShiftProductOutputReportViewModel : ObservableObject
{
    private readonly BuildShiftProductOutputReportUseCase _buildUseCase;
    private readonly ExportShiftProductOutputReportUseCase _exportUseCase;
    private readonly GetShiftProductOutputReportLookupOptionsUseCase _lookupOptionsUseCase;
    private readonly IClock _clock;
    private readonly IToastService _toastService;
    private bool _suppressTimeChanged;

    [ObservableProperty] private DateTime? _reportDate;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private string? _fromHour;
    [ObservableProperty] private string? _fromMinute;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _toHour;
    [ObservableProperty] private string? _toMinute;
    [ObservableProperty] private ObservableCollection<string> _hourOptions = [];
    [ObservableProperty] private ObservableCollection<string> _minuteOptions = [];
    [ObservableProperty] private ObservableCollection<ShiftReportShiftOption> _shiftOptions = [];
    [ObservableProperty] private ShiftReportShiftOption? _selectedShift;
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _productOptions = [];
    [ObservableProperty] private ReportLookupOptionDto? _selectedProduct;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _previewSummaryText = "Chưa có dữ liệu xem trước.";
    [ObservableProperty] private ShiftProductOutputReportDocument? _currentDocument;
    [ObservableProperty] private System.Windows.Documents.IDocumentPaginatorSource? _previewDocument;

    private System.Windows.Xps.Packaging.XpsDocument? _currentXpsDocument;
    private string? _currentTempXpsPath;
    private string? _currentTempExcelPath;

    public ShiftProductOutputReportViewModel(
        BuildShiftProductOutputReportUseCase buildUseCase,
        ExportShiftProductOutputReportUseCase exportUseCase,
        GetShiftProductOutputReportLookupOptionsUseCase lookupOptionsUseCase,
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
        ShiftOptions =
        [
            new("CA1", "Ca 1"),
            new("CA2", "Ca 2"),
            new("CA3", "Ca 3"),
            new("CUSTOM", "Tùy chỉnh")
        ];

        var now = _clock.NowLocal;
        ReportDate = now.TimeOfDay < TimeSpan.FromHours(6)
            ? now.Date.AddDays(-1)
            : now.Date;
        SelectedShift = ResolveCurrentShift(now);

        var products = await _lookupOptionsUseCase.GetProductsAsync(CancellationToken.None);
        ProductOptions = new ObservableCollection<ReportLookupOptionDto>(
            [new ReportLookupOptionDto(string.Empty, "Tất cả sản phẩm"), .. products]);
        SelectedProduct = ProductOptions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryBuildFilter(out var filter, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        try
        {
            IsBusy = true;
            CleanupOldPreview();
            var document = await _buildUseCase.ExecuteAsync(filter, CancellationToken.None);
            ApplyPreview(document);

            var result = await Helpers.ReportPreviewHelper.GeneratePreviewAsync(
                "BaoCaoSanLuongTheoCa",
                async tempPath => await _exportUseCase.ExecuteAsync(document, tempPath, CancellationToken.None));

            if (result.Success && result.XpsDocument != null)
            {
                _currentXpsDocument = result.XpsDocument;
                _currentTempExcelPath = result.ExcelPath;
                _currentTempXpsPath = result.XpsPath;
                PreviewDocument = _currentXpsDocument.GetFixedDocumentSequence();
            }
            else
            {
                _toastService.ShowWarning(result.ErrorMessage ?? "Không thể tạo bản xem trước.");
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể xem trước báo cáo sản lượng theo ca: {ex.Message}");
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

        if (!TryBuildFilter(out var filter, out var errorMessage))
        {
            _toastService.ShowWarning(errorMessage);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Tải báo cáo sản lượng theo ca",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = GetDefaultReportFolder(),
            FileName = $"BaoCaoSanLuongTheoCa_{filter.ReportDate:yyyyMMdd}_{filter.ShiftCode}_{filter.FromTime:HHmm}_{filter.ToTime:HHmm}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var document = await _buildUseCase.ExecuteAsync(filter, CancellationToken.None);
            ApplyPreview(document);
            await _exportUseCase.ExecuteAsync(document, saveDialog.FileName, CancellationToken.None);
            _toastService.ShowSuccess($"Đã tải báo cáo thành công:\n{saveDialog.FileName}");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể tải báo cáo sản lượng theo ca: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnReportDateChanged(DateTime? value)
    {
        if (SelectedShift != null && !string.Equals(SelectedShift.Code, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            ApplyShift(value ?? _clock.NowLocal.Date, SelectedShift.Code);
        }
    }

    partial void OnSelectedShiftChanged(ShiftReportShiftOption? value)
    {
        if (value == null || string.Equals(value.Code, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyShift(ReportDate ?? _clock.NowLocal.Date, value.Code);
    }

    partial void OnFromHourChanged(string? value) => MarkCustomShift();
    partial void OnFromMinuteChanged(string? value) => MarkCustomShift();
    partial void OnToHourChanged(string? value) => MarkCustomShift();
    partial void OnToMinuteChanged(string? value) => MarkCustomShift();
    partial void OnFromDateChanged(DateTime? value) => MarkCustomShift();
    partial void OnToDateChanged(DateTime? value) => MarkCustomShift();

    private void ApplyPreview(ShiftProductOutputReportDocument document)
    {
        CurrentDocument = document;
        PreviewSummaryText = $"Sản phẩm: {document.Rows.Count:N0} | Sản lượng ca: {document.GrandTotalShiftOutputTon:N3} tấn | CL/Chuyến: {document.GrandTotalReferenceCount:N0}";
    }

    private void ApplyShift(DateTime reportDate, string shiftCode)
    {
        var (fromTime, toTime) = ResolveShiftRange(reportDate.Date, shiftCode);
        _suppressTimeChanged = true;
        try
        {
            FromDate = fromTime.Date;
            FromHour = fromTime.Hour.ToString("00");
            FromMinute = fromTime.Minute.ToString("00");
            ToDate = toTime.Date;
            ToHour = toTime.Hour.ToString("00");
            ToMinute = toTime.Minute.ToString("00");
        }
        finally
        {
            _suppressTimeChanged = false;
        }
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

    private bool TryBuildFilter(out ShiftProductOutputReportFilter filter, out string errorMessage)
    {
        filter = default!;
        if (!ReportDate.HasValue)
        {
            errorMessage = "Vui lòng chọn ngày báo cáo.";
            return false;
        }

        if (!TryBuildTime(FromDate, FromHour, FromMinute, out var fromTime, out errorMessage, "Từ giờ"))
        {
            return false;
        }

        if (!TryBuildTime(ToDate, ToHour, ToMinute, out var toTime, out errorMessage, "Đến giờ"))
        {
            return false;
        }

        if (fromTime > toTime)
        {
            errorMessage = "Từ giờ không được lớn hơn Đến giờ.";
            return false;
        }

        filter = new ShiftProductOutputReportFilter(
            ReportDate.Value.Date,
            SelectedShift?.DisplayName ?? "Tùy chỉnh",
            fromTime,
            toTime.AddSeconds(59),
            string.IsNullOrWhiteSpace(SelectedProduct?.Code) ? null : SelectedProduct.Code);
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryBuildTime(DateTime? date, string? hourText, string? minuteText, out DateTime value, out string errorMessage, string label)
    {
        value = default;
        if (!date.HasValue)
        {
            errorMessage = $"Vui lòng chọn ngày cho {label}.";
            return false;
        }

        if (!int.TryParse(hourText, out var hour) || hour is < 0 or > 23)
        {
            errorMessage = $"Giờ của {label} không hợp lệ.";
            return false;
        }

        if (!int.TryParse(minuteText, out var minute) || minute is < 0 or > 59)
        {
            errorMessage = $"Phút của {label} không hợp lệ.";
            return false;
        }

        value = date.Value.Date.AddHours(hour).AddMinutes(minute);
        errorMessage = string.Empty;
        return true;
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

    private static string GetDefaultReportFolder()
    {
        var downloadsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        return Directory.Exists(downloadsFolder)
            ? downloadsFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    ~ShiftProductOutputReportViewModel()
    {
        CleanupOldPreview();
    }
}
