using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class ClayInboundReportViewModel : ObservableObject
{
    private const string LogoResourceUri = "pack://application:,,,/StationApp.UI;component/Assets/logo.jpg";

    private readonly BuildClayInboundReportUseCase _buildUseCase;
    private readonly ExportClayInboundReportUseCase _exportUseCase;
    private readonly GetClayInboundReportLookupOptionsUseCase _lookupOptionsUseCase;
    private readonly IClock _clock;
    private readonly IToastService _toastService;
    private ClayInboundReportDocument? _currentDocument;
    private bool _suppressVesselSearchSync;
    private bool _suppressProductSearchSync;
    private bool _suppressCarrierSearchSync;
    private bool _suppressFilterRefresh;
    private readonly SemaphoreSlim _dataAccessGate = new(1, 1);
    private int _vesselReloadVersion;

    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private string? _fromHour;
    [ObservableProperty] private string? _fromMinute;
    [ObservableProperty] private string? _fromSecond;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _toHour;
    [ObservableProperty] private string? _toMinute;
    [ObservableProperty] private string? _toSecond;
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _productOptions = [];
    [ObservableProperty] private ICollectionView? _productOptionsView;
    [ObservableProperty] private string? _productSearchText;
    [ObservableProperty] private ReportLookupOptionDto? _selectedProduct;
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _carrierOptions = [];
    [ObservableProperty] private ICollectionView? _carrierOptionsView;
    [ObservableProperty] private string? _carrierSearchText;
    [ObservableProperty] private ReportLookupOptionDto? _selectedCarrier;
    [ObservableProperty] private ObservableCollection<ReportLookupOptionDto> _vesselOptions = [];
    [ObservableProperty] private ICollectionView? _vesselOptionsView;
    [ObservableProperty] private string? _vesselSearchText;
    [ObservableProperty] private ReportLookupOptionDto? _selectedVessel;
    [ObservableProperty] private ObservableCollection<string> _hourOptions = [];
    [ObservableProperty] private ObservableCollection<string> _minuteOptions = [];
    [ObservableProperty] private ObservableCollection<string> _secondOptions = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<ClayInboundReportRow> _previewRows = [];
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

    ~ClayInboundReportViewModel()
    {
        CleanupOldPreview();
    }

    public ClayInboundReportViewModel(
        BuildClayInboundReportUseCase buildUseCase,
        ExportClayInboundReportUseCase exportUseCase,
        GetClayInboundReportLookupOptionsUseCase lookupOptionsUseCase,
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
        var productOptions = await _lookupOptionsUseCase.GetProductsAsync(CancellationToken.None);
        ProductOptions = new ObservableCollection<ReportLookupOptionDto>(
            new[] { new ReportLookupOptionDto(string.Empty, "Tất cả sản phẩm") }.Concat(productOptions));
        ProductOptionsView = CollectionViewSource.GetDefaultView(ProductOptions);
        ProductOptionsView.Filter = item => MatchesLookupFilter(item, ProductSearchText);

        var carrierOptions = await _lookupOptionsUseCase.GetCarriersAsync(CancellationToken.None);
        CarrierOptions = new ObservableCollection<ReportLookupOptionDto>(
            new[] { new ReportLookupOptionDto(string.Empty, "Tất cả đơn vị vận chuyển") }.Concat(carrierOptions));
        CarrierOptionsView = CollectionViewSource.GetDefaultView(CarrierOptions);
        CarrierOptionsView.Filter = item => MatchesLookupFilter(item, CarrierSearchText);

        VesselOptions = [];
        VesselOptionsView = CollectionViewSource.GetDefaultView(VesselOptions);
        VesselOptionsView.Filter = item => MatchesLookupFilter(item, VesselSearchText);
        _suppressFilterRefresh = true;
        ApplyCurrentShift();

        _suppressProductSearchSync = true;
        try
        {
            SelectedProduct = null;
            ProductSearchText = string.Empty;
            ProductOptionsView.Refresh();
        }
        finally
        {
            _suppressProductSearchSync = false;
        }

        _suppressCarrierSearchSync = true;
        try
        {
            SelectedCarrier = null;
            CarrierSearchText = string.Empty;
            CarrierOptionsView.Refresh();
        }
        finally
        {
            _suppressCarrierSearchSync = false;
        }

        _suppressVesselSearchSync = true;
        try
        {
            SelectedVessel = null;
            VesselSearchText = string.Empty;
        }
        finally
        {
            _suppressVesselSearchSync = false;
        }

        _suppressFilterRefresh = false;
        await ReloadVesselOptionsAsync();
        _currentDocument = null;
    }

    [RelayCommand]
    private async Task SearchVesselsAsync()
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
            await ReloadVesselOptionsAsync();
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể tải danh sách chuyến tàu: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
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
                "BaoCaoCanHangMoSet",
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
            Title = "Xuất báo cáo cân hàng mỏ sét",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            InitialDirectory = GetDefaultReportFolder(),
            FileName = $"BaoCaoCanHangMoSet_{fromTime:yyyyMMdd_HHmmss}_{toTime:yyyyMMdd_HHmmss}.xlsx"
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
            document.Name = $"BaoCaoCanHangMoSet_{DateTime.Now:yyyyMMddHHmmss}";

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, document.Name);
            _toastService.ShowSuccess("Đã gửi lệnh in báo cáo cân hàng mỏ sét.");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Không thể in báo cáo: {ex.Message}");
        }
    }

    private async Task<ClayInboundReportDocument> BuildDocumentFromCurrentFilterAsync()
    {
        if (!TryBuildDateRange(out var fromTime, out var toTime, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var filter = new ClayInboundReportFilter(
            fromTime,
            toTime,
            ResolveSelectedProductCode(),
            ResolveSelectedCarrierCode(),
            ResolveSelectedVesselId());

        await _dataAccessGate.WaitAsync(CancellationToken.None);
        ClayInboundReportDocument document;
        try
        {
            document = await _buildUseCase.ExecuteAsync(filter, CancellationToken.None);
        }
        finally
        {
            _dataAccessGate.Release();
        }

        var enrichedDocument = document with { LogoBytes = LoadCompanyLogoBytes() };
        _currentDocument = enrichedDocument;
        return enrichedDocument;
    }

    private void ApplyPreview(ClayInboundReportDocument document)
    {
        PreviewRows = new ObservableCollection<ClayInboundReportRow>(document.Rows);
        var vesselText = string.IsNullOrWhiteSpace(document.VesselDisplayName)
            ? string.Empty
            : $" | Chuyến tàu: {document.VesselDisplayName}";
        PreviewSummaryText = $"Số dòng: {document.Rows.Count:N0} | Hàng: {document.TotalNetWeightTon:N3} tấn | Hoàn: {document.ReturnedBrokenWeightTon:N3} tấn | Thực nhập: {document.ActualInboundWeightTon:N3} tấn{vesselText}";
    }

    partial void OnVesselSearchTextChanged(string? value)
    {
        VesselOptionsView?.Refresh();

        if (_suppressVesselSearchSync)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedVessel = null;
        }
    }

    partial void OnSelectedVesselChanged(ReportLookupOptionDto? value)
    {
        if (_suppressVesselSearchSync)
        {
            return;
        }

        var displayName = value?.DisplayName ?? string.Empty;
        if (!string.Equals(VesselSearchText, displayName, StringComparison.Ordinal))
        {
            VesselSearchText = displayName;
        }
    }

    partial void OnProductSearchTextChanged(string? value)
    {
        ProductOptionsView?.Refresh();

        if (_suppressProductSearchSync)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedProduct = null;
            ClearVesselSelection();
        }
    }

    partial void OnSelectedProductChanged(ReportLookupOptionDto? value)
    {
        if (_suppressProductSearchSync)
        {
            return;
        }

        var displayName = value?.DisplayName ?? string.Empty;
        if (!string.Equals(ProductSearchText, displayName, StringComparison.Ordinal))
        {
            ProductSearchText = displayName;
        }

        ClearVesselSelection();
    }

    partial void OnCarrierSearchTextChanged(string? value)
    {
        CarrierOptionsView?.Refresh();

        if (_suppressCarrierSearchSync)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedCarrier = null;
            ClearVesselSelection();
        }
    }

    partial void OnSelectedCarrierChanged(ReportLookupOptionDto? value)
    {
        if (_suppressCarrierSearchSync)
        {
            return;
        }

        var displayName = value?.DisplayName ?? string.Empty;
        if (!string.Equals(CarrierSearchText, displayName, StringComparison.Ordinal))
        {
            CarrierSearchText = displayName;
        }

        ClearVesselSelection();
    }

    partial void OnFromDateChanged(DateTime? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnFromHourChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnFromMinuteChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnFromSecondChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnToDateChanged(DateTime? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnToHourChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnToMinuteChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    partial void OnToSecondChanged(string? value) => ClearVesselSelectionAfterFilterChanged();
    private void ApplyCurrentShift()
    {
        var today = _clock.NowLocal.Date;
        FromDate = today;
        FromHour = "00";
        FromMinute = "00";
        FromSecond = "00";
        ToDate = today;
        ToHour = "23";
        ToMinute = "59";
        ToSecond = "59";
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

    private async Task ReloadVesselOptionsAsync()
    {
        var reloadVersion = ++_vesselReloadVersion;

        if (_suppressFilterRefresh || VesselOptionsView == null)
        {
            return;
        }

        if (!TryBuildDateRange(out var fromTime, out var toTime, out _))
        {
            SetVesselOptions([]);
            return;
        }

        var selectedVesselCode = SelectedVessel?.Code;
        var selectedProduct = SelectedProduct ?? ResolveSelectedLookup(ProductOptions, ProductSearchText);
        var productCode = selectedProduct == null || string.IsNullOrWhiteSpace(selectedProduct.Code)
            ? null
            : selectedProduct.Code;
        var selectedCarrier = SelectedCarrier ?? ResolveSelectedLookup(CarrierOptions, CarrierSearchText);
        var carrierCode = selectedCarrier == null || string.IsNullOrWhiteSpace(selectedCarrier.Code)
            ? null
            : selectedCarrier.Code;

        await _dataAccessGate.WaitAsync(CancellationToken.None);
        IReadOnlyList<ReportLookupOptionDto> vesselOptions;
        try
        {
            vesselOptions = await _lookupOptionsUseCase.GetVesselsAsync(
                new ClayInboundVesselLookupFilter(fromTime, toTime, productCode, carrierCode),
                CancellationToken.None);
        }
        finally
        {
            _dataAccessGate.Release();
        }

        if (reloadVersion != _vesselReloadVersion)
        {
            return;
        }

        SetVesselOptions(vesselOptions);

        if (!string.IsNullOrWhiteSpace(selectedVesselCode))
        {
            var matchedVessel = VesselOptions.FirstOrDefault(x => string.Equals(x.Code, selectedVesselCode, StringComparison.OrdinalIgnoreCase));
            if (matchedVessel != null)
            {
                SelectedVessel = matchedVessel;
                VesselSearchText = matchedVessel.DisplayName;
                return;
            }
        }

        _suppressVesselSearchSync = true;
        try
        {
            SelectedVessel = null;
            VesselSearchText = string.Empty;
        }
        finally
        {
            _suppressVesselSearchSync = false;
        }
    }

    private void SetVesselOptions(IEnumerable<ReportLookupOptionDto> vesselOptions)
    {
        VesselOptions = new ObservableCollection<ReportLookupOptionDto>(vesselOptions);
        VesselOptionsView = CollectionViewSource.GetDefaultView(VesselOptions);
        VesselOptionsView.Filter = item => MatchesLookupFilter(item, VesselSearchText);
        VesselOptionsView.Refresh();
    }

    private void ClearVesselSelectionAfterFilterChanged()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        ClearVesselSelection();
    }

    private void ClearVesselSelection()
    {
        if (_suppressVesselSearchSync)
        {
            return;
        }

        _vesselReloadVersion++;
        _suppressVesselSearchSync = true;
        try
        {
            SelectedVessel = null;
            VesselSearchText = string.Empty;
        }
        finally
        {
            _suppressVesselSearchSync = false;
        }
    }

    private string? ResolveSelectedProductCode()
    {
        var selectedProduct = SelectedProduct ?? ResolveSelectedLookup(ProductOptions, ProductSearchText);
        if (selectedProduct == null || string.IsNullOrWhiteSpace(selectedProduct.Code))
        {
            return null;
        }

        if (!ReferenceEquals(SelectedProduct, selectedProduct))
        {
            SelectedProduct = selectedProduct;
        }

        return selectedProduct.Code;
    }

    private string? ResolveSelectedCarrierCode()
    {
        var selectedCarrier = SelectedCarrier ?? ResolveSelectedLookup(CarrierOptions, CarrierSearchText);
        if (selectedCarrier == null || string.IsNullOrWhiteSpace(selectedCarrier.Code))
        {
            return null;
        }

        if (!ReferenceEquals(SelectedCarrier, selectedCarrier))
        {
            SelectedCarrier = selectedCarrier;
        }

        return selectedCarrier.Code;
    }

    private Guid? ResolveSelectedVesselId()
    {
        var selectedVessel = SelectedVessel ?? ResolveSelectedLookup(VesselOptions, VesselSearchText);
        if (selectedVessel == null || string.IsNullOrWhiteSpace(selectedVessel.Code))
        {
            return null;
        }

        if (!Guid.TryParse(selectedVessel.Code, out var vesselId))
        {
            return null;
        }

        if (!ReferenceEquals(SelectedVessel, selectedVessel))
        {
            SelectedVessel = selectedVessel;
        }

        return vesselId;
    }

    private static ReportLookupOptionDto? ResolveSelectedLookup(
        IEnumerable<ReportLookupOptionDto> options,
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var keyword = text.Trim();
        return options.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.Code)
            && (string.Equals(x.Code, keyword, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.DisplayName, keyword, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool MatchesLookupFilter(object item, string? filterText)
    {
        if (item is not ReportLookupOptionDto option)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        var keyword = filterText.Trim();
        return option.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || option.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
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

    private bool TryBuildDateRange(out DateTime fromTime, out DateTime toTime, out string errorMessage)
    {
        if (!FromDate.HasValue)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Vui lòng chọn Từ ngày.";
            return false;
        }

        if (!ToDate.HasValue)
        {
            fromTime = default;
            toTime = default;
            errorMessage = "Vui lòng chọn Đến ngày.";
            return false;
        }

        fromTime = FromDate.Value.Date;
        toTime = ToDate.Value.Date.AddDays(1).AddTicks(-1);

        if (fromTime > toTime)
        {
            errorMessage = "Từ ngày không được lớn hơn Đến ngày.";
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

    private static FlowDocument BuildPrintDocument(ClayInboundReportDocument document)
    {
        var flowDocument = new FlowDocument
        {
            FontFamily = new FontFamily("Times New Roman"),
            FontSize = 11
        };

        var headerTable = new Table();
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(76) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(264) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(360) });

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
            Text = "BÁO CÁO CÂN HÀNG MỎ SÉT",
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

        if (!string.IsNullOrWhiteSpace(document.VesselDisplayName))
        {
            flowDocument.Blocks.Add(new Paragraph(new Run($"Chuyến tàu: {document.VesselDisplayName}"))
            {
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var dataTable = new Table { CellSpacing = 0 };
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(34) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(76) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(56) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(92) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(58) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(58) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(58) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(58) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(64) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(112) });
        dataTable.Columns.Add(new TableColumn { Width = new GridLength(94) });

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
        AddCell(reportHeaderRow, "Hoàn (tấn)", true, TextAlignment.Center, Brushes.Red);
        AddCell(reportHeaderRow, "Thực nhập (tấn)", true);
        AddCell(reportHeaderRow, "Khách hàng", true);
        AddCell(reportHeaderRow, "Hàng hóa", true);

        for (var index = 0; index < document.Rows.Count; index++)
        {
            var row = document.Rows[index];
            var dataRow = new TableRow();
            dataGroup.Rows.Add(dataRow);
            AddCell(dataRow, row.RowNo.ToString());
            AddCell(dataRow, row.SessionNo);
            AddCell(dataRow, row.InternalVehicleNo);
            AddCell(dataRow, row.Weight2Time?.ToString("dd/MM/yyyy HH:mm"));
            AddCell(dataRow, row.GrossWeightTon.ToString("N3"), false, TextAlignment.Right);
            AddCell(dataRow, row.TareWeightTon.ToString("N3"), false, TextAlignment.Right);
            AddCell(dataRow, row.NetWeightTon.ToString("N3"), false, TextAlignment.Right);
            AddCell(dataRow, row.ReturnedBrokenWeightTon > 0 ? row.ReturnedBrokenWeightTon.ToString("N3") : string.Empty, false, TextAlignment.Right, Brushes.Red);
            AddCell(dataRow, row.ActualInboundWeightTon.ToString("N3"), false, TextAlignment.Right);
            AddCell(dataRow, row.CustomerName);
            AddCell(dataRow, row.ProductName);
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
        AddCell(totalRow, document.ReturnedBrokenWeightTon.ToString("N3"), false, TextAlignment.Right, Brushes.Red);
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
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 8, 0, 0),
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
            Foreground = foreground ?? Brushes.Black,
            TextAlignment = textAlignment,
            Background = isHeader ? new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9)) : null
        });
    }

    private static string BuildTimeRangeText(DateTime fromTime, DateTime toTime)
    {
        if (fromTime.Date == toTime.Date)
        {
            return $"Ngày: {fromTime:dd/MM/yyyy}";
        }

        return $"Từ ngày {fromTime:dd/MM/yyyy} đến ngày {toTime:dd/MM/yyyy}";
    }
}


