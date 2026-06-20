using System.Globalization;
using StationApp.Application.Formatting;
using StationApp.Domain.Entities;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;

namespace StationApp.Application.Printing;

public enum PrintDocumentKind
{
    WeighTicket,
    DeliveryTicket
}

public enum PrintWrapMode
{
    NoWrap,
    Wrap,
    Trim
}

public enum PrintFieldAlignment
{
    Left,
    Center,
    Right
}

public enum PrintFieldWeight
{
    Normal,
    SemiBold,
    Bold
}

public sealed record PrinterDescriptor(string Name, bool IsDefault);

public sealed record PrintFieldDefinition(
    string FieldKey,
    double X,
    double Y,
    double Width,
    PrintFieldAlignment Alignment,
    double FontSize,
    PrintFieldWeight FontWeight,
    int MaxLines = 1,
    PrintWrapMode WrapMode = PrintWrapMode.Trim,
    string? LiteralValue = null,
    double RotationDegrees = 0d,
    bool Underline = false,
    bool Italic = false,
    bool ShadedBackground = false,
    bool IsImage = false,
    string? ImageSourceUri = null,
    bool IsLine = false,
    bool IsEnabled = true);

public sealed record PrintFieldValue(string FieldKey, string? Value);
public sealed record PrintFieldPosition(string FieldKey, double X, double Y, double? Width = null, bool IsEnabled = true);
public sealed record PrintTemplateProfileDescriptor(string ProfileKey, string DisplayName, bool IsDefault);

public sealed class PrintTemplateDefinition
{
    public required PrintDocumentKind Kind { get; init; }
    public required string TemplateName { get; init; }
    public double PageWidthMm { get; init; } = 210d;
    public double PageHeightMm { get; init; } = 297d;
    public double DefaultOffsetXmm { get; init; }
    public double DefaultOffsetYmm { get; init; }
    public string? ActiveProfileKey { get; init; }
    public string? ActiveProfileName { get; init; }
    public required IReadOnlyList<PrintFieldDefinition> Fields { get; init; }
}

public abstract class PrintPreviewPageModel
{
    public required Guid DocumentId { get; init; }
    public required string DisplayNumber { get; init; }
    public required IReadOnlyList<PrintFieldValue> Fields { get; init; }
    public string? PreviewGroupKey { get; set; }
    public string? PreviewGroupName { get; set; }
}

public sealed class WeighTicketPrintModel : PrintPreviewPageModel
{
    public required string TicketNo { get; init; }
    public required string VehiclePlate { get; init; }
    public string? MoocNumber { get; init; }
    public decimal? NetWeight { get; init; }
}

public sealed class DeliveryTicketPrintModel : PrintPreviewPageModel
{
    public required string DeliveryNo { get; init; }
    public string? OrderCode { get; init; }
    public decimal? ActualWeight { get; init; }
}

public sealed class PrintBatchPreviewModel
{
    public required PrintDocumentKind Kind { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<PrintPreviewPageModel> Pages { get; init; }
}

public sealed class PrintOptionsModel
{
    public string? SelectedPrinterName { get; init; }
    public int CopyCount { get; init; } = 1;
    public double OffsetXmm { get; init; }
    public double OffsetYmm { get; init; }
    public IReadOnlyList<PrintFieldPosition> FieldPositions { get; init; } = [];
    public string? SelectedFieldKey { get; init; }
    public IReadOnlyList<Guid> SelectedDocumentIds { get; init; } = [];
}

public sealed record PrintDocumentResult(Guid DocumentId, string DisplayNumber, bool Success, string? ErrorMessage = null);

public sealed class PrintExecutionResult
{
    public required IReadOnlyList<PrintDocumentResult> Documents { get; init; }
    public bool HasFailures => Documents.Any(d => !d.Success);
}

public interface IWeighTicketPrintComposer
{
    WeighTicketPrintModel Compose(
        CutOrder registration,
        WeighTicket ticket,
        Vehicle? vehicle,
        DateTime printedAtLocal,
        string? printedByDisplayName);
}

public interface IDeliveryTicketPrintComposer
{
    DeliveryTicketPrintModel Compose(
        CutOrder registration,
        DeliveryTicket deliveryTicket,
        WeighTicket? weighTicket,
        WeighingSessionLine? sessionLine,
        Vehicle? vehicle,
        DateTime printedAtLocal,
        string? printedByDisplayName);
}

public interface IPrintTemplateProvider
{
    Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, CancellationToken ct);
    Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, string? profileKey, CancellationToken ct);
    Task<IReadOnlyList<PrintTemplateProfileDescriptor>> GetProfilesAsync(PrintDocumentKind kind, CancellationToken ct);
    Task SaveLayoutAsync(
        PrintDocumentKind kind,
        string? profileKey,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct);
    Task<PrintTemplateProfileDescriptor> CreateProfileAsync(
        PrintDocumentKind kind,
        string displayName,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct);
    Task SetDefaultProfileAsync(PrintDocumentKind kind, string profileKey, CancellationToken ct);
    Task<string> ExportBackupAsync(CancellationToken ct);
}

public interface IPrinterDiscoveryService
{
    IReadOnlyList<PrinterDescriptor> GetInstalledPrinters();
    bool PrinterExists(string? printerName);
}

public interface IPrintService
{
    Task<PrintExecutionResult> PrintAsync(
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        PrintOptionsModel options,
        CancellationToken ct);
}

public sealed class WeighTicketPrintComposer : IWeighTicketPrintComposer
{
    public WeighTicketPrintModel Compose(
        CutOrder registration,
        WeighTicket ticket,
        Vehicle? vehicle,
        DateTime printedAtLocal,
        string? printedByDisplayName)
    {
        var emptyWeight = ticket.TransactionType == TransactionType.OUTBOUND ? ticket.Weight1 : ticket.Weight2;
        var grossWeight = ticket.TransactionType == TransactionType.OUTBOUND ? ticket.Weight2 : ticket.Weight1;
        var vehicleLine = BuildVehicleLine(
            FirstNonEmpty(ticket.VehiclePlate, registration.VehiclePlate),
            FirstNonEmpty(ticket.MoocNumber, registration.MoocNumber));

        return new WeighTicketPrintModel
        {
            DocumentId = ticket.Id,
            DisplayNumber = BusinessNumberFormatter.ToDisplay(ticket.TicketNo),
            TicketNo = ticket.TicketNo,
            VehiclePlate = vehicleLine,
            MoocNumber = ticket.MoocNumber ?? registration.MoocNumber,
            NetWeight = ticket.NetWeight,
            Fields = new[]
            {
                Field("TicketNo", BusinessNumberFormatter.ToDisplay(ticket.TicketNo)),
                Field("VehiclePlate", vehicleLine),
                Field("VehicleRegistrationNo", FirstNonEmpty(ticket.VehicleRegistrationNoSnapshot, vehicle?.VehicleRegistrationNo)),
                Field("MoocRegistrationNo", FirstNonEmpty(ticket.MoocRegistrationNoSnapshot, vehicle?.MoocRegistrationNo)),
                Field("CustomerName", string.Equals(ticket.RecordRole, WeighTicketRecordRoles.MasterSession, StringComparison.OrdinalIgnoreCase)
                    ? FirstNonEmpty(registration.CustomerName, ticket.CustomerName)
                    : FirstNonEmpty(ticket.CustomerName, registration.CustomerName)),
                Field("ProductName", (string.Equals(ticket.RecordRole, WeighTicketRecordRoles.MasterSession, StringComparison.OrdinalIgnoreCase)
                    ? FirstNonEmpty(registration.ProductName, ticket.ProductName)
                    : FirstNonEmpty(ticket.ProductName, registration.ProductName))?.ToUpperInvariant()),
                Field("LotNo", registration.LotNo),
                Field("RepresentativeName", registration.RepresentativeName),
                Field("Notes", string.Equals(ticket.RecordRole, WeighTicketRecordRoles.MasterSession, StringComparison.OrdinalIgnoreCase)
                    ? FirstNonEmpty(registration.Notes, ticket.Notes)
                    : FirstNonEmpty(ticket.Notes, registration.Notes)),
                Field("Weight1DateTime", FormatDateTimeWithSeconds(ticket.Weight1Time)),
                Field("Weight2DateTime", FormatDateTimeWithSeconds(ticket.Weight2Time)),
                Field("EmptyWeight", FormatWeight(emptyWeight)),
                Field("GrossWeight", FormatWeight(grossWeight)),
                Field("NetWeight", FormatWeight(ticket.NetWeight)),
                Field("PrintedAt", FormatDateTimePrinted(printedAtLocal)),
                Field("PrintedBy", printedByDisplayName)
            }
        };
    }

    private static PrintFieldValue Field(string key, string? value) => new(key, value);
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    private static string BuildVehicleLine(string? vehiclePlate, string? moocNumber)
    {
        if (string.IsNullOrWhiteSpace(moocNumber))
        {
            return vehiclePlate ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(vehiclePlate)
            ? moocNumber
            : $"{vehiclePlate} ({moocNumber})";
    }

    private static string? FormatDateTimeWithSeconds(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    private static string? FormatDateTimePrinted(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    private static string? FormatWeight(decimal? value)
        => value.HasValue ? (value.Value / 1000m).ToString("0.0##", CultureInfo.InvariantCulture) : null;
}

public sealed class DeliveryTicketPrintComposer : IDeliveryTicketPrintComposer
{
    public DeliveryTicketPrintModel Compose(
        CutOrder registration,
        DeliveryTicket deliveryTicket,
        WeighTicket? weighTicket,
        WeighingSessionLine? sessionLine,
        Vehicle? vehicle,
        DateTime printedAtLocal,
        string? printedByDisplayName)
    {
        var vehicleLine = string.Join(Environment.NewLine, new[] { FirstNonEmpty(weighTicket?.VehiclePlate, registration.VehiclePlate), FirstNonEmpty(weighTicket?.MoocNumber, registration.MoocNumber) }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
        var actualWeight = deliveryTicket.AllocatedWeight ?? sessionLine?.ActualAllocatedWeight ?? weighTicket?.NetWeight;
        var isBagged = string.Equals(ProductTypes.Normalize(registration.ProductType), ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
        var plannedBagCount = isBagged ? registration.BagCount?.ToString(CultureInfo.InvariantCulture) : null;
        var actualBagCount = isBagged
            ? (deliveryTicket.AllocatedBagCount ?? sessionLine?.ActualAllocatedBagCount)?.ToString(CultureInfo.InvariantCulture)
            : null;

        return new DeliveryTicketPrintModel
        {
            DocumentId = deliveryTicket.Id,
            DisplayNumber = BusinessNumberFormatter.ToDisplay(deliveryTicket.DeliveryNo),
            DeliveryNo = deliveryTicket.DeliveryNo,
            OrderCode = FirstNonEmpty(registration.OrderCode, deliveryTicket.ErpCutOrderId, registration.ErpCutOrderId),
            ActualWeight = actualWeight,
            Fields = new[]
            {
                Field("DeliveryNo", BusinessNumberFormatter.ToDisplay(deliveryTicket.DeliveryNo)),
                Field("ReferenceCode", FirstNonEmpty(registration.OrderCode, deliveryTicket.ErpCutOrderId, registration.ErpCutOrderId)),
                Field("CustomerName", registration.CustomerName),
                Field("CustomerCode", registration.CustomerCode),
                Field("ProductName", registration.ProductName),
                Field("Market", registration.Market),
                Field("ConsumptionPlace", registration.ConsumptionPlace),
                Field("LoadingPlace", registration.LoadingPlace),
                Field("LotNo", registration.LotNo),
                Field("SealNo", registration.SealNo),
                Field("PlannedWeight", FormatWeight(registration.PlannedWeight)),
                Field("BagCount", plannedBagCount),
                Field("ActualWeight", FormatWeight(actualWeight)),
                Field("ActualBagCount", actualBagCount),
                Field("VehicleLine", vehicleLine),
                Field("VehicleRegistrationNo", FirstNonEmpty(weighTicket?.VehicleRegistrationNoSnapshot, vehicle?.VehicleRegistrationNo)),
                Field("MoocRegistrationNo", FirstNonEmpty(weighTicket?.MoocRegistrationNoSnapshot, vehicle?.MoocRegistrationNo)),
                Field("Notes", FirstNonEmpty(deliveryTicket.Notes, registration.Notes)),
                Field("Weight1Hour", FormatHour(weighTicket?.Weight1Time)),
                Field("Weight1Minute", FormatMinute(weighTicket?.Weight1Time)),
                Field("Weight1Date", FormatDateOnly(weighTicket?.Weight1Time)),
                Field("Weight2Hour", FormatHour(weighTicket?.Weight2Time)),
                Field("Weight2Minute", FormatMinute(weighTicket?.Weight2Time)),
                Field("Weight2Date", FormatDateOnly(weighTicket?.Weight2Time)),
                Field("PrintedDate", FormatDateOnly(printedAtLocal)),
                Field("PrintedTime", printedAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture)),
                Field("PrintedBy", printedByDisplayName)
            }
        };
    }

    private static PrintFieldValue Field(string key, string? value) => new(key, value);
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    private static string? FormatHour(DateTime? value) => value?.ToString("HH", CultureInfo.InvariantCulture);
    private static string? FormatMinute(DateTime? value) => value?.ToString("mm", CultureInfo.InvariantCulture);
    private static string? FormatDateOnly(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string? FormatDateOnly(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string? FormatWeight(decimal? value)
        => value.HasValue ? (value.Value / 1000m).ToString("0.0##", CultureInfo.InvariantCulture) : null;
}

