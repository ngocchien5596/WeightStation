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
    private PrintTemplateDefinition _template;
    private readonly PrintBatchPreviewModel _batch;
    private Dictionary<string, PrintFieldDefinition> _fieldDefaults;
    private bool _normalizingLayoutMessage;

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
    [ObservableProperty] private ObservableCollection<PrintTemplateProfileDescriptor> _profiles = new();
    [ObservableProperty] private PrintTemplateProfileDescriptor? _selectedProfile;
    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private string? _backupFilePath;
    [ObservableProperty] private ObservableCollection<PrintPreviewSelectionItem> _previewItems = new();
    [ObservableProperty] private PrintPreviewSelectionItem? _selectedPreviewItem;
    [ObservableProperty] private ObservableCollection<PrintFieldEditorItem> _fields = new();
    [ObservableProperty] private PrintFieldEditorItem? _selectedField;
    [ObservableProperty] private double _nudgeStepMm = 0.5d;
    [ObservableProperty] private string _nudgeStepMmText = "0.5";
    [ObservableProperty] private bool _canManageLayout;

    public PrintOptionsModel? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public bool CanPrint => SelectedPrinter != null && CopyCount > 0;
    public bool HasSelectedField => SelectedField != null;
    public bool CanManageProfiles => CanManageLayout && SelectedProfile != null;
    public string PreviewHeader => CanManageLayout
        ? "Preview canh chỉnh vị trí in"
        : "Preview trước khi in";
    public string PreviewDescription => CanManageLayout
        ? "Chọn text bên phải, chỉnh X/Y hoặc dịch từng bước, preview sẽ cập nhật ngay."
        : "Kiểm tra lại nội dung phiếu trước khi in. Tài khoản hiện tại không có quyền thay đổi layout.";
    public string RoleAwareDescription => CanManageLayout
        ? "Bạn có thể lưu bộ vị trí chuẩn, tạo version mẫu in mới và đổi mẫu in mặc định."
        : "Tài khoản OPERATOR chỉ được preview và in theo layout đã được ADMIN chuẩn hóa trước đó.";

    partial void OnLayoutMessageChanged(string? value)
    {
        if (_normalizingLayoutMessage || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var backupIndex = value.IndexOf("Backup:", StringComparison.OrdinalIgnoreCase);
        if (backupIndex < 0)
        {
            return;
        }

        var backupPath = value[(backupIndex + "Backup:".Length)..].Trim();
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            backupPath = BackupFilePath ?? string.Empty;
        }

        _normalizingLayoutMessage = true;
        try
        {
            LayoutMessage = string.IsNullOrWhiteSpace(backupPath)
                ? "Đã cập nhật cấu hình in trong DB."
                : $"Đã cập nhật cấu hình in trong DB. File backup: {backupPath}";
        }
        finally
        {
            _normalizingLayoutMessage = false;
        }
    }

    public PrintOptionsDialogViewModel(
        string title,
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        IReadOnlyList<PrintTemplateProfileDescriptor> profiles,
        IReadOnlyList<PrinterDescriptor> printers,
        PrintOverlayRenderer renderer,
        IPrintTemplateProvider templateProvider,
        bool canManageLayout,
        int defaultCopyCount = 1)
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
        SelectedPrinter = Printers.FirstOrDefault(x => x.IsDefault) ?? Printers.FirstOrDefault();
        CopyCount = defaultCopyCount > 0 ? defaultCopyCount : 1;

        Profiles = new ObservableCollection<PrintTemplateProfileDescriptor>(profiles);
        SelectedProfile = Profiles.FirstOrDefault(x => string.Equals(x.ProfileKey, template.ActiveProfileKey, StringComparison.OrdinalIgnoreCase))
            ?? Profiles.FirstOrDefault();

        OffsetXmm = template.DefaultOffsetXmm;
        OffsetYmm = template.DefaultOffsetYmm;
        OffsetXmmText = FormatEditableNumber(OffsetXmm);
        OffsetYmmText = FormatEditableNumber(OffsetYmm);
        NudgeStepMmText = FormatEditableNumber(NudgeStepMm);

        PreviewItems = new ObservableCollection<PrintPreviewSelectionItem>(BuildPreviewItems(batch));
        Fields = new ObservableCollection<PrintFieldEditorItem>(template.Fields.Select(field => CreateFieldEditor(template.Kind, field)));
        foreach (var field in Fields)
        {
            field.PropertyChanged += OnFieldPropertyChanged;
        }

        SelectedPreviewItem = GetDefaultPreviewItem(PreviewItems);
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
        OnPropertyChanged(nameof(CanManageProfiles));
        NudgeLeftCommand.NotifyCanExecuteChanged();
        NudgeRightCommand.NotifyCanExecuteChanged();
        NudgeUpCommand.NotifyCanExecuteChanged();
        NudgeDownCommand.NotifyCanExecuteChanged();
        ResetSelectedFieldCommand.NotifyCanExecuteChanged();
        SetDefaultProfileCommand.NotifyCanExecuteChanged();
        RefreshPreview();
    }

    partial void OnSelectedPreviewItemChanged(PrintPreviewSelectionItem? value)
    {
        RefreshPreview();
    }

    partial void OnSelectedProfileChanged(PrintTemplateProfileDescriptor? value)
    {
        OnPropertyChanged(nameof(CanManageProfiles));
        SetDefaultProfileCommand.NotifyCanExecuteChanged();
        _ = LoadSelectedProfileAsync(value);
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
            SelectedFieldKey = SelectedField?.FieldKey,
            SelectedDocumentIds = GetSelectedPages().Select(x => x.DocumentId).ToList()
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
                SelectedProfile?.ProfileKey,
                OffsetXmm,
                OffsetYmm,
                positions,
                CancellationToken.None);

            foreach (var position in positions)
            {
                if (_fieldDefaults.TryGetValue(position.FieldKey, out var field))
                {
                    _fieldDefaults[position.FieldKey] = field with
                    {
                        X = position.X,
                        Y = position.Y,
                        Width = position.Width ?? field.Width,
                        IsEnabled = position.IsEnabled
                    };
                }
            }

            BackupFilePath = await _templateProvider.ExportBackupAsync(CancellationToken.None);
            LayoutMessage = $"Đã lưu layout. Backup: {BackupFilePath}";
            ValidationMessage = null;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CreateProfileAsync()
    {
        if (!CanManageLayout)
        {
            return;
        }

        try
        {
            var profileName = string.IsNullOrWhiteSpace(NewProfileName)
                ? GetSuggestedProfileName()
                : NewProfileName.Trim();

            var profile = await _templateProvider.CreateProfileAsync(
                _template.Kind,
                profileName,
                OffsetXmm,
                OffsetYmm,
                BuildFieldPositions(),
                CancellationToken.None);

            Profiles.Add(profile);
            SelectedProfile = profile;
            NewProfileName = string.Empty;
            BackupFilePath = await _templateProvider.ExportBackupAsync(CancellationToken.None);
            LayoutMessage = $"Đã tạo mẫu in {profile.DisplayName}. Backup: {BackupFilePath}";
            ValidationMessage = null;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageProfiles))]
    private async Task SetDefaultProfileAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        try
        {
            await _templateProvider.SetDefaultProfileAsync(_template.Kind, SelectedProfile.ProfileKey, CancellationToken.None);
            var refreshed = Profiles
                .Select(x => x with { IsDefault = string.Equals(x.ProfileKey, SelectedProfile.ProfileKey, StringComparison.OrdinalIgnoreCase) })
                .ToList();
            Profiles = new ObservableCollection<PrintTemplateProfileDescriptor>(refreshed);
            SelectedProfile = Profiles.First(x => x.IsDefault);
            BackupFilePath = await _templateProvider.ExportBackupAsync(CancellationToken.None);
            LayoutMessage = $"Đã đặt {SelectedProfile.DisplayName} làm mẫu mặc định. Backup: {BackupFilePath}";
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
        SelectedField.WidthValue = RoundPosition(field.Width);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrintFieldEditorItem.X)
            or nameof(PrintFieldEditorItem.Y)
            or nameof(PrintFieldEditorItem.WidthValue)
            or nameof(PrintFieldEditorItem.IsEnabled))
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
            GetSelectedPages(),
            new PrintOptionsModel
            {
                CopyCount = 1,
                OffsetXmm = OffsetXmm,
                OffsetYmm = OffsetYmm,
                FieldPositions = BuildFieldPositions(),
                SelectedFieldKey = SelectedField?.FieldKey,
                SelectedDocumentIds = GetSelectedPages().Select(x => x.DocumentId).ToList()
            },
            previewMode: true);
    }

    private IReadOnlyList<PrintPreviewPageModel> GetSelectedPages()
    {
        if (SelectedPreviewItem == null || SelectedPreviewItem.IsAll)
        {
            return _batch.Pages;
        }

        if (SelectedPreviewItem.IsGroup)
        {
            return _batch.Pages
                .Where(x => string.Equals(x.PreviewGroupKey, SelectedPreviewItem.GroupKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return _batch.Pages;
    }

    private async Task LoadSelectedProfileAsync(PrintTemplateProfileDescriptor? profile)
    {
        if (profile == null || string.Equals(profile.ProfileKey, _template.ActiveProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var template = await _templateProvider.GetTemplateAsync(_template.Kind, profile.ProfileKey, CancellationToken.None);
            ApplyTemplate(template);
            ValidationMessage = null;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    private void ApplyTemplate(PrintTemplateDefinition template)
    {
        _template = template;
        _fieldDefaults = template.Fields.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        OffsetXmm = template.DefaultOffsetXmm;
        OffsetYmm = template.DefaultOffsetYmm;
        OffsetXmmText = FormatEditableNumber(OffsetXmm);
        OffsetYmmText = FormatEditableNumber(OffsetYmm);

        foreach (var field in Fields)
        {
            field.PropertyChanged -= OnFieldPropertyChanged;
        }

        Fields = new ObservableCollection<PrintFieldEditorItem>(template.Fields.Select(field => CreateFieldEditor(template.Kind, field)));
        foreach (var field in Fields)
        {
            field.PropertyChanged += OnFieldPropertyChanged;
        }

        SelectedField = Fields.FirstOrDefault();
        RefreshPreview();
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
            ActiveProfileKey = _template.ActiveProfileKey,
            ActiveProfileName = _template.ActiveProfileName,
            Fields = _template.Fields
        };

    private IReadOnlyList<PrintFieldPosition> BuildFieldPositions()
        => Fields
            .Select(x => new PrintFieldPosition(
                x.FieldKey,
                RoundPosition(x.X),
                RoundPosition(x.Y),
                RoundPosition(x.WidthValue),
                x.IsEnabled))
            .ToList();

    private string GetSuggestedProfileName()
    {
        var prefix = _template.Kind == PrintDocumentKind.WeighTicket ? "PC ver " : "PGN ver ";
        var maxNumber = Profiles
            .Select(x => x.DisplayName)
            .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(x =>
            {
                var suffix = x[prefix.Length..].Trim();
                return int.TryParse(suffix, out var number) ? number : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxNumber + 1}";
    }

    private static PrintFieldEditorItem CreateFieldEditor(PrintDocumentKind kind, PrintFieldDefinition field)
        => new()
        {
            FieldKey = field.FieldKey,
            DisplayName = BuildDisplayName(kind, field),
            FieldType = BuildFieldType(field),
            IsEnabled = field.IsEnabled,
            X = RoundPosition(field.X),
            Y = RoundPosition(field.Y),
            XText = FormatEditableNumber(field.X),
            YText = FormatEditableNumber(field.Y),
            WidthValue = RoundPosition(field.Width),
            Width = FormatEditableNumber(field.Width)
        };

    private static string BuildFieldType(PrintFieldDefinition field)
    {
        if (field.IsImage || field.IsLine || !string.IsNullOrWhiteSpace(field.LiteralValue))
        {
            return "Tĩnh";
        }

        return "Động";
    }

    private static string BuildDisplayName(PrintDocumentKind kind, PrintFieldDefinition field)
    {
        if (field.IsImage)
        {
            return field.FieldKey.Equals("StaticCompanyLogo", StringComparison.OrdinalIgnoreCase)
                ? "Logo công ty"
                : field.FieldKey;
        }

        if (!string.IsNullOrWhiteSpace(field.LiteralValue))
        {
            return field.LiteralValue;
        }

        var weighMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TicketNo"] = "Số phiếu",
            ["VehiclePlate"] = "Số PTVC",
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
            ["CustomerName"] = "T\u00EAn kh\u00E1ch h\u00E0ng",
            ["Market"] = "Th\u1ECB tr\u01B0\u1EDDng",
            ["ConsumptionPlace"] = "Th\u1ECB tr\u01B0\u1EDDng ti\u00EAu th\u1EE5",
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
            ["PrintedBy"] = "Người giao hàng",
            ["Notes"] = "Ghi chú"
        };

        var map = kind == PrintDocumentKind.WeighTicket ? weighMap : deliveryMap;
        return map.TryGetValue(field.FieldKey, out var displayName) ? displayName : field.FieldKey;
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

    private static IReadOnlyList<PrintPreviewSelectionItem> BuildPreviewItems(PrintBatchPreviewModel batch)
    {
        var items = new List<PrintPreviewSelectionItem>
        {
            new()
            {
                DisplayName = "Tất cả phiếu",
                IsAll = true
            }
        };

        items.AddRange(batch.Pages
            .Where(page => !string.IsNullOrWhiteSpace(page.PreviewGroupKey) && !string.IsNullOrWhiteSpace(page.PreviewGroupName))
            .GroupBy(page => page.PreviewGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PrintPreviewSelectionItem
            {
                DisplayName = group.First().PreviewGroupName!,
                GroupKey = group.Key,
                IsGroup = true
            }));

        return items;
    }

    private static PrintPreviewSelectionItem? GetDefaultPreviewItem(IEnumerable<PrintPreviewSelectionItem> items)
    {
        var list = items.ToList();
        return list.FirstOrDefault(x => x.IsGroup && string.Equals(x.GroupKey, "weigh-master", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(x => x.IsGroup && string.Equals(x.GroupKey, "delivery-master", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(x => x.IsGroup)
            ?? list.FirstOrDefault();
    }
}

public sealed class PrintPreviewSelectionItem
{
    public Guid DocumentId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsAll { get; init; }
    public bool IsGroup { get; init; }
    public string? GroupKey { get; init; }
}

public partial class PrintFieldEditorItem : ObservableObject
{
    [ObservableProperty] private string _fieldKey = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _fieldType = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _widthValue;
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

    partial void OnWidthValueChanged(double value)
    {
        Width = value.ToString("0.##", CultureInfo.InvariantCulture);
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

    partial void OnWidthChanged(string value)
    {
        if (TryParseEditableNumber(value, out var parsed))
        {
            SetProperty(ref _widthValue, Math.Round(parsed, 2, MidpointRounding.AwayFromZero), nameof(WidthValue));
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
