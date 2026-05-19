using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.UI.Printing;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels.Settings;

public partial class PrintConfigViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty] private string? _lastBackupFilePath;
    [ObservableProperty] private string? _lastBackupFileDisplay;
    [ObservableProperty] private string? _printerSettingsMessage;
    [ObservableProperty] private ObservableCollection<PrinterDescriptor> _weighTicketPrinters = new();
    [ObservableProperty] private ObservableCollection<PrinterDescriptor> _deliveryTicketPrinters = new();
    [ObservableProperty] private PrinterDescriptor? _selectedWeighTicketPrinter;
    [ObservableProperty] private PrinterDescriptor? _selectedDeliveryTicketPrinter;

    public bool CanManagePrintLayout => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);

    public PrintConfigViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
    }

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
        var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        try
        {
            LastBackupFilePath = await templateProvider.ExportBackupAsync(CancellationToken.None);
            LastBackupFileDisplay = BuildBackupDisplay(LastBackupFilePath);
        }
        catch
        {
            LastBackupFilePath = null;
            LastBackupFileDisplay = null;
        }

        var installedPrinters = printerDiscovery.GetInstalledPrinters();
        var weighTicketDefault = await appConfig.GetValueAsync(AppConfigKeys.DefaultWeighTicketPrinter, CancellationToken.None);
        var deliveryTicketDefault = await appConfig.GetValueAsync(AppConfigKeys.DefaultDeliveryTicketPrinter, CancellationToken.None);

        WeighTicketPrinters = new ObservableCollection<PrinterDescriptor>(
            PrinterSelectionHelper.ApplyPreferredPrinter(installedPrinters, weighTicketDefault));
        DeliveryTicketPrinters = new ObservableCollection<PrinterDescriptor>(
            PrinterSelectionHelper.ApplyPreferredPrinter(installedPrinters, deliveryTicketDefault));

        SelectedWeighTicketPrinter = WeighTicketPrinters.FirstOrDefault(x => x.IsDefault) ?? WeighTicketPrinters.FirstOrDefault();
        SelectedDeliveryTicketPrinter = DeliveryTicketPrinters.FirstOrDefault(x => x.IsDefault) ?? DeliveryTicketPrinters.FirstOrDefault();
        PrinterSettingsMessage = null;
    }

    [RelayCommand]
    private Task ConfigureWeighTicketAsync() => OpenConfigDialogAsync(PrintDocumentKind.WeighTicket);

    [RelayCommand]
    private Task ConfigureDeliveryTicketAsync() => OpenConfigDialogAsync(PrintDocumentKind.DeliveryTicket);

    [RelayCommand]
    private async Task SavePrinterDefaultsAsync()
    {
        if (!CanManagePrintLayout)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await appConfig.SetValueAsync(
            AppConfigKeys.DefaultWeighTicketPrinter,
            SelectedWeighTicketPrinter?.Name ?? string.Empty,
            CancellationToken.None);
        await appConfig.SetValueAsync(
            AppConfigKeys.DefaultDeliveryTicketPrinter,
            SelectedDeliveryTicketPrinter?.Name ?? string.Empty,
            CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        PrinterSettingsMessage = "\u0110\u00E3 l\u01B0u m\u00E1y in m\u1EB7c \u0111\u1ECBnh ri\u00EAng cho phi\u1EBF\u0301u c\u00E2n v\u00E0 phi\u1EBF\u0301u giao nh\u1EADn.";
    }

    private async Task OpenConfigDialogAsync(PrintDocumentKind kind)
    {
        if (!CanManagePrintLayout)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
        var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var renderer = scope.ServiceProvider.GetRequiredService<PrintOverlayRenderer>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        var template = await templateProvider.GetTemplateAsync(kind, CancellationToken.None);
        var profiles = await templateProvider.GetProfilesAsync(kind, CancellationToken.None);
        var preview = BuildSamplePreview(kind);
        var preferredPrinter = await appConfig.GetValueAsync(GetPrinterConfigKey(kind), CancellationToken.None);
        var printers = PrinterSelectionHelper.ApplyPreferredPrinter(
            printerDiscovery.GetInstalledPrinters(),
            preferredPrinter);

        var vm = new PrintOptionsDialogViewModel(
            kind == PrintDocumentKind.WeighTicket
                ? "C\u1EA5u h\u00ECnh m\u1EABu in phi\u1EBF\u0301u c\u00E2n"
                : "C\u1EA5u h\u00ECnh m\u1EABu in phi\u1EBF\u0301u giao nh\u1EADn",
            template,
            preview,
            profiles,
            printers,
            renderer,
            templateProvider,
            true);

        await dialogService.ShowCustomDialogAsync<PrintOptionsDialogViewModel, PrintOptionsModel>(vm);
        LastBackupFilePath = await templateProvider.ExportBackupAsync(CancellationToken.None);
        LastBackupFileDisplay = BuildBackupDisplay(LastBackupFilePath);
    }

    private static string BuildBackupDisplay(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Chưa có file backup";
        }

        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName)
            ? "File backup cạnh thư mục chạy ứng dụng"
            : $"{fileName} (cạnh file .exe của ứng dụng)";
    }

    private static string GetPrinterConfigKey(PrintDocumentKind kind)
        => kind == PrintDocumentKind.WeighTicket
            ? AppConfigKeys.DefaultWeighTicketPrinter
            : AppConfigKeys.DefaultDeliveryTicketPrinter;

    private static PrintBatchPreviewModel BuildSamplePreview(PrintDocumentKind kind)
    {
        var fields = kind == PrintDocumentKind.WeighTicket
            ? BuildWeighFields()
            : BuildDeliveryFields();

        return new PrintBatchPreviewModel
        {
            Kind = kind,
            Title = kind == PrintDocumentKind.WeighTicket ? "Phi\u1EBF\u0301u c\u00E2n m\u1EABu" : "Phi\u1EBF\u0301u giao nh\u1EADn m\u1EABu",
            Pages =
            [
                new SamplePrintPreviewPageModel
                {
                    DocumentId = Guid.NewGuid(),
                    DisplayNumber = kind == PrintDocumentKind.WeighTicket ? "PC_SAMPLE_01" : "PGN_SAMPLE_01",
                    Fields = fields
                }
            ]
        };
    }

    private static IReadOnlyList<PrintFieldValue> BuildWeighFields() =>
    [
        new("TicketNo", "PC26050001"),
        new("VehiclePlate", "14C-25678"),
        new("VehicleRegistrationNo", "43-123456"),
        new("MoocRegistrationNo", "43R-654321"),
        new("CustomerName", "C\u00D4NG TY TNHH TH\u01AF\u01A0NG M\u1EA0I H\u1EA0 LONG"),
        new("ProductName", "XI M\u0102NG R\u1EDCI PCB50"),
        new("LotNo", "L001"),
        new("RepresentativeName", "Nguy\u1EC5n V\u0103n A"),
        new("Notes", "Ghi ch\u00FA m\u1EABu"),
        new("Weight1DateTime", "17/05/2026 23:01:44"),
        new("Weight2DateTime", "17/05/2026 23:02:13"),
        new("EmptyWeight", "2.5"),
        new("GrossWeight", "16.8"),
        new("NetWeight", "14.3"),
        new("PrintedAt", "18/05/2026 09:10"),
        new("PrintedBy", "B\u00F9i Ng\u1ECDc Chi\u1EBFn")
    ];

    private static IReadOnlyList<PrintFieldValue> BuildDeliveryFields() =>
    [
        new("DeliveryNo", "PGN26050001"),
        new("ReferenceCode", "QN.DKPT.2605/0201"),
        new("CustomerName", "C\u00D4NG TY TNHH TH\u01AF\u01A0NG M\u1EA0I H\u1EA0 LONG"),
        new("ConsumptionPlace", "H\u1EA1 Long"),
        new("LoadingPlace", "XMCP"),
        new("CustomerCode", "KH001"),
        new("ProductName", "XI M\u0102NG R\u1EDCI PCB50"),
        new("LotNo", "L001"),
        new("PlannedWeight", "14.0"),
        new("BagCount", "280"),
        new("ActualWeight", "14.3"),
        new("ActualBagCount", "286"),
        new("VehicleLine", "14C-25678\n14R-01316"),
        new("SealNo", "NC001"),
        new("Weight1Hour", "14"),
        new("Weight1Minute", "55"),
        new("Weight1Date", "17/05/2026"),
        new("Weight2Hour", "15"),
        new("Weight2Minute", "10"),
        new("Weight2Date", "17/05/2026"),
        new("Notes", "Ghi ch\u00FA m\u1EABu"),
        new("PrintedBy", "B\u00F9i Ng\u1ECDc Chi\u1EBFn")
    ];

    private sealed class SamplePrintPreviewPageModel : PrintPreviewPageModel
    {
    }
}
