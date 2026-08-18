using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class CrusherInboundReportViewModel : ObservableObject
{
    private const string LogoResourceUri = "pack://application:,,,/StationApp.UI;component/Assets/logo.jpg";

    private readonly BuildCrusherInboundReportUseCase _buildUseCase;
    private readonly ExportCrusherInboundReportUseCase _exportUseCase;
    private readonly IClock _clock;
    private readonly IToastService _toastService;
    private CrusherInboundReportDocument? _currentDocument;
    private bool _suppressTimeChanged;

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private string? _fromHour;
    [ObservableProperty] private string? _fromMinute;
    [ObservableProperty] private string? _fromSecond;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _toHour;
    [ObservableProperty] private string? _toMinute;
    [ObservableProperty] private string? _toSecond;
    [ObservableProperty] private string? _vehicleSearchText;
    [ObservableProperty] private ObservableCollection<string> _hourOptions = [];
    [ObservableProperty] private ObservableCollection<string> _minuteOptions = [];
    [ObservableProperty] private ObservableCollection<string> _secondOptions = [];
    [ObservableProperty] private ObservableCollection<ShiftReportShiftOption> _shiftOptions = [];
    [ObservableProperty] private ShiftReportShiftOption? _selectedShift;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<CrusherInboundReportRow> _previewRows = [];
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

    ~CrusherInboundReportViewModel()
    {
        CleanupOldPreview();
    }

    public CrusherInboundReportViewModel(
        BuildCrusherInboundReportUseCase buildUseCase,
        ExportCrusherInboundReportUseCase exportUseCase,
        IClock clock,
        IToastService toastService)
    {
        _buildUseCase = buildUseCase;
        _exportUseCase = exportUseCase;
        _clock = clock;
        _toastService = toastService;
    }

    public Task InitializeAsync()
    {
        HourOptions = new ObservableCollection<string>(Enumerable.Range(0, 24).Select(x => x.ToString("00")));
        MinuteOptions = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(x => x.ToString("00")));
        SecondOptions = new ObservableCollection<string>(Enumerable.Range(0, 60).Select(x => x.ToString("00")));
        ShiftOptions =
        [
            new("CA1", "Ca 1"),
            new("CA2", "Ca 2"),
            new("CUSTOM", "Tùy chỉnh")
        ];
        ApplyCurrentShift();
        VehicleSearchText = null;
        _currentDocument = null;
        return Task.CompletedTask;
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
                "BaoCaoCanHangTramDap",
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
            Title = "Xuất báo cáo cân hàng mỏ đá",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = GetDefaultReportFolder(),
            FileName = $"BaoCaoCanHangTramDap_{fromTime:yyyyMMdd_HHmmss}_{toTime:yyyyMMdd_HHmmss}.xlsx"
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

    [RelayCommand]
    private void Print()
    {
        if (_currentDocument == null || _currentDocument.Rows.Count == 0)
        {
            _toastService.ShowWarning("Chưa có dữ liệu trên grid để in.");
            return;
        }

        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            var document = BuildPrintDocument(_currentDocument);
            document.PageWidth = printDialog.PrintableAreaWidth;
            document.PageHeight = printDialog.PrintableAreaHeight;
            document.ColumnWidth = printDialog.PrintableAreaWidth;
            document.PagePadding = new Thickness(18);
            document.Name = $"BaoCaoCanHangTramDap_{DateTime.Now:yyyyMMddHHmmss}";

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, document.Name);
            _toastService.ShowSuccess("Đã gửi lệnh in báo cáo cân hàng mỏ đá.");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể in báo cáo: {ex.Message}");
        }
    }

    private async Task<CrusherInboundReportDocument> BuildDocumentFromCurrentFilterAsync()
    {
        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var filter = new CrusherInboundReportFilter(
            fromTime,
            toTime,
            string.IsNullOrWhiteSpace(VehicleSearchText) ? null : VehicleSearchText.Trim());

        var document = await _buildUseCase.ExecuteAsync(filter, CancellationToken.None);
        var enrichedDocument = document with { LogoBytes = LoadCompanyLogoBytes() };
        _currentDocument = enrichedDocument;
        return enrichedDocument;
    }

    private void ApplyPreview(CrusherInboundReportDocument document)
    {
        PreviewRows = new ObservableCollection<CrusherInboundReportRow>(document.Rows);
        PreviewSummaryText = $"Số dòng: {document.Rows.Count:N0} | Nhập: {document.TotalNetWeightTon:N3} tấn | Hoàn: {document.ReturnedBrokenWeightTon:N3} tấn | Thực nhập: {document.ActualInboundWeightTon:N3} tấn";
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

        if (timeOfDay >= TimeSpan.FromHours(5) && timeOfDay < TimeSpan.FromHours(12))
        {
            return (today.AddHours(5), today.AddHours(12).AddSeconds(-1));
        }

        if (timeOfDay >= TimeSpan.FromHours(12) && timeOfDay <= TimeSpan.FromHours(21))
        {
            return (today.AddHours(12), today.AddHours(21));
        }

        if (timeOfDay > TimeSpan.FromHours(21))
        {
            return (today.AddHours(12), today.AddHours(21));
        }

        return (today.AddHours(5), today.AddHours(12).AddSeconds(-1));
    }

    private ShiftReportShiftOption ResolveCurrentShift(DateTime now)
    {
        var code = now.TimeOfDay >= TimeSpan.FromHours(12) && now.TimeOfDay <= TimeSpan.FromHours(21)
            ? "CA2"
            : "CA1";

        return ShiftOptions.First(x => x.Code == code);
    }

    private static (DateTime FromTime, DateTime ToTime) ResolveShiftRange(DateTime reportDate, string shiftCode)
        => shiftCode switch
        {
            "CA2" => (reportDate.AddHours(12), reportDate.AddHours(21)),
            _ => (reportDate.AddHours(5), reportDate.AddHours(12).AddSeconds(-1))
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
            errorMessage = "Giây của Từ giờ không hợp lệ.";
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
            errorMessage = "Giây của Đến giờ không hợp lệ.";
            return false;
        }

        fromTime = FromDate.Value.Date
            .AddHours(fromHour)
            .AddMinutes(fromMinute)
            .AddSeconds(fromSecond);
        toTime = ToDate.Value.Date
            .AddHours(toHour)
            .AddMinutes(toMinute)
            .AddSeconds(toSecond);

        if (fromTime > toTime)
        {
            errorMessage = "Từ giờ không được lớn hơn Đến giờ.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static byte[]? LoadCompanyLogoBytes()
    {
        try
        {
            var resourceInfo = System.Windows.Application.GetResourceStream(new Uri(LogoResourceUri, UriKind.Absolute));
            if (resourceInfo?.Stream == null)
            {
                return null;
            }

            using var stream = resourceInfo.Stream;
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? BuildLogoImageSource(byte[]? logoBytes)
    {
        if (logoBytes is not { Length: > 0 })
        {
            return null;
        }

        var image = new BitmapImage();
        using var stream = new MemoryStream(logoBytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static FlowDocument BuildPrintDocument(CrusherInboundReportDocument document)
    {
        var flowDocument = new FlowDocument
        {
            FontFamily = new FontFamily("Times New Roman"),
            FontSize = 11
        };

        var headerTable = new Table();
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(76) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(264) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(270) });

        var headerGroup = new TableRowGroup();
        headerTable.RowGroups.Add(headerGroup);
        var headerRow = new TableRow();
        headerGroup.Rows.Add(headerRow);

        var logoSource = BuildLogoImageSource(document.LogoBytes);
        var logoElement = new Image
        {
            Source = logoSource,
            Width = 68,
            Height = 60,
            Stretch = Stretch.Uniform
        };
        headerRow.Cells.Add(new TableCell(new BlockUIContainer(logoElement))
        {
            BorderThickness = new Thickness(0),
            TextAlignment = TextAlignment.Center
        });

        var leftHeaderPanel = new StackPanel();
        leftHeaderPanel.Children.Add(new TextBlock
        {
            Text = "CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ",
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        });
        leftHeaderPanel.Children.Add(new TextBlock { Text = "Địa chỉ: Km6, Quốc lộ 18A, Quang Hanh, Quảng Ninh" });
        leftHeaderPanel.Children.Add(new TextBlock { Text = "Điện thoại: (84-203) 3.721.995 - (84-203) 3.721.996" });
        headerRow.Cells.Add(new TableCell(new BlockUIContainer(leftHeaderPanel)) { BorderThickness = new Thickness(0) });

        var rightHeaderPanel = new StackPanel();
        rightHeaderPanel.Children.Add(new TextBlock
        {
            Text = "BÁO CÁO CÂN HÀNG",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        });
        rightHeaderPanel.Children.Add(new TextBlock
        {
            Text = BuildTimeRangeText(document.FromTime, document.ToTime),
            Margin = new Thickness(0, 8, 0, 0)
        });
        headerRow.Cells.Add(new TableCell(new BlockUIContainer(rightHeaderPanel)) { BorderThickness = new Thickness(0) });

        flowDocument.Blocks.Add(headerTable);
        flowDocument.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 10) });

        var dataTable = new Table();
        dataTable.CellSpacing = 0;
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(38) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(78) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(64) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(88) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(62) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(62) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(62) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(62) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(74) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(116) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(86) });

        var dataGroup = new TableRowGroup();
        dataTable.RowGroups.Add(dataGroup);

        var reportHeaderRow = new TableRow();
        dataGroup.Rows.Add(reportHeaderRow);
        AddCell(reportHeaderRow, "STT", true);
        AddCell(reportHeaderRow, "Số phiếu", true);
        AddCell(reportHeaderRow, "Số xe", true);
        AddCell(reportHeaderRow, "Ngày cân", true);
        AddCell(reportHeaderRow, "Tổng (tấn)", true);
        AddCell(reportHeaderRow, "Bì (tấn)", true);
        AddCell(reportHeaderRow, "Hàng (tấn)", true);
        AddCell(reportHeaderRow, "Khách hàng", true);
        AddCell(reportHeaderRow, "Hàng hóa", true);

        AddCell(reportHeaderRow, "Hoàn", true);

        reportHeaderRow.Cells.Clear();
        AddCell(reportHeaderRow, "STT", true);
        AddCell(reportHeaderRow, "Số phiếu", true);
        AddCell(reportHeaderRow, "Số xe", true);
        AddCell(reportHeaderRow, "Ngày cân", true);
        AddCell(reportHeaderRow, "Tổng (tấn)", true);
        AddCell(reportHeaderRow, "Bì (tấn)", true);
        AddCell(reportHeaderRow, "Hàng (tấn)", true);
        AddCell(reportHeaderRow, "Hoàn (tấn)", true, foreground: Brushes.Firebrick);
        AddCell(reportHeaderRow, "Thực nhập (tấn)", true);
        AddCell(reportHeaderRow, "Khách hàng", true);
        AddCell(reportHeaderRow, "Hàng hóa", true);

        for (var index = 0; index < document.Rows.Count; index++)
        {
            var row = document.Rows[index];
            var dataRow = new TableRow();
            dataGroup.Rows.Add(dataRow);
            var rowBrush = row.IsReturnedBrokenTrip ? Brushes.Firebrick : null;
            AddCell(dataRow, row.RowNo.ToString(), foreground: rowBrush);
            AddCell(dataRow, row.SessionNo, foreground: rowBrush);
            AddCell(dataRow, row.InternalVehicleNo, foreground: rowBrush);
            AddCell(dataRow, row.Weight2Time?.ToString("dd/MM/yyyy HH:mm"), foreground: rowBrush);
            AddCell(dataRow, row.GrossWeightTon.ToString("N3"), foreground: rowBrush);
            AddCell(dataRow, row.TareWeightTon.ToString("N3"), foreground: rowBrush);
            AddCell(dataRow, row.NetWeightTon.ToString("N3"), foreground: rowBrush);
            AddCell(dataRow, row.ReturnedBrokenWeightTon == 0m ? string.Empty : row.ReturnedBrokenWeightTon.ToString("N3"), foreground: Brushes.Firebrick);
            AddCell(dataRow, row.ActualInboundWeightTon.ToString("N3"), foreground: rowBrush);
            AddCell(dataRow, row.CustomerName, foreground: rowBrush);
            AddCell(dataRow, row.ProductName, foreground: rowBrush);
        }

        var totalRow = new TableRow();
        dataGroup.Rows.Add(totalRow);
        var totalCell = new TableCell(new Paragraph(new Run("Cộng tổng:")))
        {
            ColumnSpan = 4,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(4),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        totalRow.Cells.Add(totalCell);
        AddCell(totalRow, string.Empty);
        AddCell(totalRow, string.Empty);
        AddCell(totalRow, document.TotalNetWeightTon.ToString("N3"), false, TextAlignment.Right);
        AddCell(totalRow, document.ReturnedBrokenWeightTon.ToString("N3"), false, TextAlignment.Right, Brushes.Firebrick);
        AddCell(totalRow, document.ActualInboundWeightTon.ToString("N3"), false, TextAlignment.Right);
        AddCell(totalRow, string.Empty);
        AddCell(totalRow, string.Empty);

        flowDocument.Blocks.Add(dataTable);
        flowDocument.Blocks.Add(new Paragraph { Margin = new Thickness(0, 6, 0, 0) });

        var signatureTable = new Table();
        signatureTable.Columns.Add(new TableColumn());
        signatureTable.Columns.Add(new TableColumn());
        var signatureGroup = new TableRowGroup();
        signatureTable.RowGroups.Add(signatureGroup);

        var signatureTitleRow = new TableRow();
        signatureGroup.Rows.Add(signatureTitleRow);
        signatureTitleRow.Cells.Add(new TableCell(new Paragraph(new Run("ĐẠI DIỆN ĐƠN VỊ KHAI THÁC")))
        {
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        });
        signatureTitleRow.Cells.Add(new TableCell(new Paragraph(new Run("ĐẠI DIỆN PHÂN XƯỞNG KHAI THÁC")))
        {
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        });

        var signatureSpacerRow = new TableRow();
        signatureGroup.Rows.Add(signatureSpacerRow);
        signatureSpacerRow.Cells.Add(new TableCell(new Paragraph(new Run(" "))) { BorderThickness = new Thickness(0), Padding = new Thickness(0, 28, 0, 28) });
        signatureSpacerRow.Cells.Add(new TableCell(new Paragraph(new Run(" "))) { BorderThickness = new Thickness(0), Padding = new Thickness(0, 28, 0, 28) });

        var signatureNameRow = new TableRow();
        signatureGroup.Rows.Add(signatureNameRow);
        signatureNameRow.Cells.Add(new TableCell(new Paragraph(new Run(string.Empty))) { BorderThickness = new Thickness(0) });
        signatureNameRow.Cells.Add(new TableCell(new Paragraph(new Run(document.PreparedByDisplayName)))
        {
            BorderThickness = new Thickness(0),
            TextAlignment = TextAlignment.Center
        });

        flowDocument.Blocks.Add(signatureTable);

        var footerTable = new Table();
        footerTable.Columns.Add(new TableColumn());
        footerTable.Columns.Add(new TableColumn());
        footerTable.Columns.Add(new TableColumn());
        var footerGroup = new TableRowGroup();
        footerTable.RowGroups.Add(footerGroup);
        var footerRow = new TableRow();
        footerGroup.Rows.Add(footerRow);
        footerRow.Cells.Add(CreateFooterCell(document.StationName, true, TextAlignment.Left));
        footerRow.Cells.Add(CreateFooterCell($"Thời gian in: {DateTime.Now:dd/MM/yyyy HH:mm}", false, TextAlignment.Center, true));
        footerRow.Cells.Add(CreateFooterCell("Trang: 1/1", false, TextAlignment.Right));
        flowDocument.Blocks.Add(footerTable);

        return flowDocument;
    }

    private static TableCell CreateFooterCell(string text, bool bold, TextAlignment alignment, bool italic = false)
    {
        return new TableCell(new Paragraph(new Run(text)))
        {
            BorderBrush = Brushes.Black,
            Padding = new Thickness(0, 8, 0, 0),
            BorderThickness = new Thickness(0, 1, 0, 0),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Regular,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            TextAlignment = alignment
        };
    }

    private static void AddCell(
        TableRow row,
        string? text,
        bool isHeader = false,
        TextAlignment textAlignment = TextAlignment.Center,
        Brush? foreground = null)
    {
        row.Cells.Add(new TableCell(new Paragraph(new Run(text ?? string.Empty)))
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(4),
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Regular,
            TextAlignment = textAlignment,
            Background = isHeader ? new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9)) : null,
            Foreground = foreground ?? Brushes.Black
        });
    }

    private static string BuildTimeRangeText(DateTime fromTime, DateTime toTime)
    {
        return $"Từ giờ {fromTime:HH:mm:ss dd/MM/yyyy} đến giờ {toTime:HH:mm:ss dd/MM/yyyy}";
    }
}

