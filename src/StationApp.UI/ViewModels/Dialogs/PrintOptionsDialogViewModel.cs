using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.Printing;
using StationApp.UI.Printing;

namespace StationApp.UI.ViewModels.Dialogs;

public partial class PrintOptionsDialogViewModel : ObservableObject
{
    private readonly PrintOverlayRenderer _renderer;
    private readonly IPrintTemplateProvider _templateProvider;
    private readonly PrintTemplateDefinition _template;
    private readonly PrintBatchPreviewModel _batch;
    private readonly Dictionary<string, PrintFieldDefinition> _fieldDefaults;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private ObservableCollection<PrinterDescriptor> _printers = new();
    [ObservableProperty] private PrinterDescriptor? _selectedPrinter;
    [ObservableProperty] private int _copyCount = 1;
    [ObservableProperty] private double _offsetXmm;
    [ObservableProperty] private double _offsetYmm;
    [ObservableProperty] private string _offsetXmmText = "0";
    [ObservableProperty] private string _offsetYmmText = "0";
    [ObservableProperty] private IDocumentPaginatorSource? _previewDocument;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private string? _layoutMessage;
    [ObservableProperty] private ObservableCollection<PrintFieldEditorItem> _fields = new();
    [ObservableProperty] private PrintFieldEditorItem? _selectedField;
    [ObservableProperty] private double _nudgeStepMm = 0.5d;
    [ObservableProperty] private string _nudgeStepMmText = "0.5";
    [ObservableProperty] private bool _canManageLayout;

    public PrintOptionsModel? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public bool CanPrint => SelectedPrinter != null && CopyCount > 0;
    public bool HasSelectedField => SelectedField != null;
    public string PreviewHeader => CanManageLayout
        ? "Preview canh chỉnh vị trí in"
        : "Preview trước khi in";
    public string PreviewDescription => CanManageLayout
        ? "Chọn text bên phải, chỉnh X/Y hoặc dịch từng bước, preview sẽ cập nhật ngay."
        : "Kiểm tra lại nội dung phiếu trước khi in. Tài khoản hiện tại không có quyền thay đổi layout.";
    public string RoleAwareDescription => CanManageLayout
        ? "Bạn có thể lưu bộ vị trí chuẩn. Các lần preview / in sau sẽ dùng lại layout này."
        : "Tài khoản OPERATOR chỉ được preview và in theo layout đã được ADMIN chuẩn hóa trước đó.";

    public PrintOptionsDialogViewModel(
        string title,
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        IReadOnlyList<PrinterDescriptor> printers,
        PrintOverlayRenderer renderer,
        IPrintTemplateProvider templateProvider,
        bool canManageLayout)
    {
        Title = title;
        _template = template;
        _batch = batch;
        _renderer = renderer;
        _templateProvider = templateProvider;
        _fieldDefaults = template.Fields.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        CanManageLayout = canManageLayout;
        OnPropertyChanged(nameof(PreviewHeader));
        OnPropertyChanged(nameof(PreviewDescription));
        OnPropertyChanged(nameof(RoleAwareDescription));
        Printers = new ObservableCollection<PrinterDescriptor>(printers);
        SelectedPrinter = Printers.FirstOrDefault(p => p.IsDefault) ?? Printers.FirstOrDefault();
        OffsetXmm = template.DefaultOffsetXmm;
        OffsetYmm = template.DefaultOffsetYmm;
        OffsetXmmText = FormatEditableNumber(OffsetXmm);
        OffsetYmmText = FormatEditableNumber(OffsetYmm);
        NudgeStepMmText = FormatEditableNumber(NudgeStepMm);
        Fields = new ObservableCollection<PrintFieldEditorItem>(template.Fields.Select(field => CreateFieldEditor(template.Kind, field)));
        foreach (var field in Fields)
        {
            field.PropertyChanged += OnFieldPropertyChanged;
        }

        SelectedField = Fields.FirstOrDefault();
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

    partial void OnOffsetXmmChanged(double value)
    {
        LayoutMessage = null;
        OffsetXmmText = FormatEditableNumber(value);
        RefreshPreview();
    }

    partial void OnOffsetYmmChanged(double value)
    {
        LayoutMessage = null;
        OffsetYmmText = FormatEditableNumber(value);
        RefreshPreview();
    }

    partial void OnOffsetXmmTextChanged(string value)
    {
        LayoutMessage = null;
        if (TryParseEditableNumber(value, out var parsed))
        {
            SetProperty(ref _offsetXmm, RoundPosition(parsed), nameof(OffsetXmm));
        }
    }

    partial void OnOffsetYmmTextChanged(string value)
    {
        LayoutMessage = null;
        if (TryParseEditableNumber(value, out var parsed))
        {
            SetProperty(ref _offsetYmm, RoundPosition(parsed), nameof(OffsetYmm));
        }
    }

    partial void OnNudgeStepMmTextChanged(string value)
    {
        if (TryParseEditableNumber(value, out var parsed) && parsed > 0)
        {
            SetProperty(ref _nudgeStepMm, RoundPosition(parsed), nameof(NudgeStepMm));
        }
    }

    partial void OnSelectedFieldChanged(PrintFieldEditorItem? value)
    {
        LayoutMessage = null;
        OnPropertyChanged(nameof(HasSelectedField));
        NudgeLeftCommand.NotifyCanExecuteChanged();
        NudgeRightCommand.NotifyCanExecuteChanged();
        NudgeUpCommand.NotifyCanExecuteChanged();
        NudgeDownCommand.NotifyCanExecuteChanged();
        ResetSelectedFieldCommand.NotifyCanExecuteChanged();
        RefreshPreview();
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        DialogResultValue = new PrintOptionsModel
        {
            SelectedPrinterName = SelectedPrinter?.Name,
            CopyCount = CopyCount,
            OffsetXmm = OffsetXmm,
            OffsetYmm = OffsetYmm,
            FieldPositions = BuildFieldPositions(),
            SelectedFieldKey = SelectedField?.FieldKey
        };

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private async Task SaveLayoutAsync()
    {
        if (!CanManageLayout)
        {
            ValidationMessage = "Tài khoản hiện tại không có quyền chỉnh layout in.";
            return;
        }

        try
        {
            var positions = BuildFieldPositions();
            await _templateProvider.SaveLayoutAsync(
                _template.Kind,
                OffsetXmm,
                OffsetYmm,
                positions,
                CancellationToken.None);

            foreach (var position in positions)
            {
                if (_fieldDefaults.TryGetValue(position.FieldKey, out var field))
                {
                    _fieldDefaults[position.FieldKey] = field with { X = position.X, Y = position.Y };
                }
            }

            LayoutMessage = "Đã lưu bộ căn chỉnh vị trí in.";
            ValidationMessage = null;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void NudgeLeft()
    {
        if (SelectedField == null || !CanManageLayout)
        {
            return;
        }

        SelectedField.X = RoundPosition(SelectedField.X - NudgeStepMm);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void NudgeRight()
    {
        if (SelectedField == null || !CanManageLayout)
        {
            return;
        }

        SelectedField.X = RoundPosition(SelectedField.X + NudgeStepMm);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void NudgeUp()
    {
        if (SelectedField == null || !CanManageLayout)
        {
            return;
        }

        SelectedField.Y = RoundPosition(SelectedField.Y - NudgeStepMm);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void NudgeDown()
    {
        if (SelectedField == null || !CanManageLayout)
        {
            return;
        }

        SelectedField.Y = RoundPosition(SelectedField.Y + NudgeStepMm);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void ResetSelectedField()
    {
        if (SelectedField == null || !CanManageLayout || !_fieldDefaults.TryGetValue(SelectedField.FieldKey, out var field))
        {
            return;
        }

        SelectedField.X = RoundPosition(field.X);
        SelectedField.Y = RoundPosition(field.Y);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrintFieldEditorItem.X) or nameof(PrintFieldEditorItem.Y))
        {
            if (!CanManageLayout)
            {
                return;
            }

            LayoutMessage = null;
            RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        PreviewDocument = _renderer.CreateDocument(
            BuildPreviewTemplate(),
            _batch.Pages,
            new PrintOptionsModel
            {
                CopyCount = 1,
                OffsetXmm = OffsetXmm,
                OffsetYmm = OffsetYmm,
                FieldPositions = BuildFieldPositions(),
                SelectedFieldKey = SelectedField?.FieldKey
            },
            previewMode: true);
    }

    private PrintTemplateDefinition BuildPreviewTemplate()
        => new()
        {
            Kind = _template.Kind,
            TemplateName = _template.TemplateName,
            PageWidthMm = _template.PageWidthMm,
            PageHeightMm = _template.PageHeightMm,
            DefaultOffsetXmm = _template.DefaultOffsetXmm,
            DefaultOffsetYmm = _template.DefaultOffsetYmm,
            Fields = _template.Fields
        };

    private IReadOnlyList<PrintFieldPosition> BuildFieldPositions()
        => Fields
            .Select(x => new PrintFieldPosition(x.FieldKey, RoundPosition(x.X), RoundPosition(x.Y)))
            .ToList();

    private static PrintFieldEditorItem CreateFieldEditor(PrintDocumentKind kind, PrintFieldDefinition field)
        => new()
        {
            FieldKey = field.FieldKey,
            DisplayName = BuildDisplayName(kind, field.FieldKey),
            X = RoundPosition(field.X),
            Y = RoundPosition(field.Y),
            XText = FormatEditableNumber(field.X),
            YText = FormatEditableNumber(field.Y),
            Width = field.Width.ToString("0.##", CultureInfo.InvariantCulture)
        };

    private static string BuildDisplayName(PrintDocumentKind kind, string fieldKey)
    {
        var weighMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TicketNo"] = "Số phiếu",
            ["VehiclePlate"] = "Biển số xe",
            ["MoocNumber"] = "Số moóc",
            ["VehicleRegistrationNo"] = "Tem xe",
            ["MoocRegistrationNo"] = "Tem moóc",
            ["CustomerName"] = "Khách hàng",
            ["ProductName"] = "Hàng hóa",
            ["LotNo"] = "Lô hàng",
            ["RepresentativeName"] = "Đại diện",
            ["Notes"] = "Ghi chú",
            ["Weight1DateTime"] = "Giờ vào",
            ["Weight2DateTime"] = "Giờ ra",
            ["EmptyWeight"] = "Trọng lượng xe (tấn)",
            ["GrossWeight"] = "Trọng lượng tổng (tấn)",
            ["NetWeight"] = "Trọng lượng hàng (tấn)",
            ["PrintedAt"] = "Ngày in phiếu",
            ["PrintedBy"] = "Nhân viên cân"
        };

        var deliveryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DeliveryNo"] = "Số phiếu",
            ["ReferenceCode"] = "Mã đơn hàng",
            ["CustomerName"] = "Tên khách hàng",
            ["ConsumptionPlace"] = "Nơi tiêu thụ",
            ["LoadingPlace"] = "Nơi xuất hàng",
            ["CustomerCode"] = "Mã khách hàng",
            ["ProductName"] = "Chủng loại mặt hàng",
            ["LotNo"] = "Số lô",
            ["PlannedWeight"] = "Số lượng đặt hàng (Tấn)",
            ["BagCount"] = "Số lượng đặt hàng (Bao)",
            ["ActualWeight"] = "Số lượng thực giao (Tấn)",
            ["ActualBagCount"] = "Số lượng thực giao (Bao)",
            ["VehicleLine"] = "Tên phương tiện vận tải",
            ["SealNo"] = "Niêm chì số",
            ["Weight1Hour"] = "Thời gian làm thủ tục cho phương tiện vào nhận hàng (Giờ)",
            ["Weight1Minute"] = "Thời gian làm thủ tục cho phương tiện vào nhận hàng (Phút)",
            ["Weight1Date"] = "Thời gian làm thủ tục cho phương tiện vào nhận hàng (Ngày)",
            ["Weight2Hour"] = "Thời gian hoàn tất thủ tục giao hàng cho phương tiện (Giờ)",
            ["Weight2Minute"] = "Thời gian hoàn tất thủ tục giao hàng cho phương tiện (Phút)",
            ["Weight2Date"] = "Thời gian hoàn tất thủ tục giao hàng cho phương tiện (Ngày)",
            ["PrintedDate"] = "Ngày in phiếu",
            ["PrintedTime"] = "Giờ in phiếu",
            ["PrintedBy"] = "Người giao hàng",
            ["Notes"] = "Ghi chú"
        };

        var map = kind == PrintDocumentKind.WeighTicket ? weighMap : deliveryMap;
        return map.TryGetValue(fieldKey, out var displayName) ? displayName : fieldKey;
    }

    private static bool TryParseEditableNumber(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().Replace(',', '.');
        if (normalized.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatEditableNumber(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static double RoundPosition(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public partial class PrintFieldEditorItem : ObservableObject
{
    [ObservableProperty] private string _fieldKey = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private string _xText = "0";
    [ObservableProperty] private string _yText = "0";
    [ObservableProperty] private string _width = string.Empty;

    partial void OnXChanged(double value)
    {
        XText = value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    partial void OnYChanged(double value)
    {
        YText = value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    partial void OnXTextChanged(string value)
    {
        if (TryParseEditableNumber(value, out var parsed))
        {
            SetProperty(ref _x, Math.Round(parsed, 2, MidpointRounding.AwayFromZero), nameof(X));
        }
    }

    partial void OnYTextChanged(string value)
    {
        if (TryParseEditableNumber(value, out var parsed))
        {
            SetProperty(ref _y, Math.Round(parsed, 2, MidpointRounding.AwayFromZero), nameof(Y));
        }
    }

    private static bool TryParseEditableNumber(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().Replace(',', '.');
        if (normalized.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
