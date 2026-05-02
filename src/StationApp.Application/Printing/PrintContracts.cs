using System.Globalization;
using StationApp.Domain.Entities;
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
    PrintWrapMode WrapMode = PrintWrapMode.Trim);

public sealed record PrintFieldValue(string FieldKey, string? Value);

public sealed class PrintTemplateDefinition
{
    public required PrintDocumentKind Kind { get; init; }
    public required string TemplateName { get; init; }
    public double PageWidthMm { get; init; } = 210d;
    public double PageHeightMm { get; init; } = 297d;
    public double DefaultOffsetXmm { get; init; }
    public double DefaultOffsetYmm { get; init; }
    public required IReadOnlyList<PrintFieldDefinition> Fields { get; init; }
}

public abstract class PrintPreviewPageModel
{
    public required Guid DocumentId { get; init; }
    public required string DisplayNumber { get; init; }
    public required IReadOnlyList<PrintFieldValue> Fields { get; init; }
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
    public string? CutOrderCode { get; init; }
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
}

public sealed record PrintDocumentResult(Guid DocumentId, string DisplayNumber, bool Success, string? ErrorMessage = null);

public sealed class PrintExecutionResult
{
    public required IReadOnlyList<PrintDocumentResult> Documents { get; init; }
    public bool HasFailures => Documents.Any(d => !d.Success);
}

public interface IWeighTicketPrintComposer
{
    WeighTicketPrintModel Compose(VehicleRegistration registration, WeighTicket ticket, Vehicle? vehicle, DateTime printedAtLocal);
}

public interface IDeliveryTicketPrintComposer
{
    DeliveryTicketPrintModel Compose(
        VehicleRegistration registration,
        DeliveryTicket deliveryTicket,
        WeighTicket? weighTicket,
        WeighingSessionLine? sessionLine,
        Vehicle? vehicle,
        DateTime printedAtLocal);
}

public interface IPrintTemplateProvider
{
    Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, CancellationToken ct);
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
    public WeighTicketPrintModel Compose(VehicleRegistration registration, WeighTicket ticket, Vehicle? vehicle, DateTime printedAtLocal)
    {
        var emptyWeight = ticket.TransactionType == TransactionType.OUTBOUND ? ticket.Weight1 : ticket.Weight2;
        var grossWeight = ticket.TransactionType == TransactionType.OUTBOUND ? ticket.Weight2 : ticket.Weight1;

        return new WeighTicketPrintModel
        {
            DocumentId = ticket.Id,
            DisplayNumber = ticket.TicketNo,
            TicketNo = ticket.TicketNo,
            VehiclePlate = ticket.VehiclePlate,
            MoocNumber = ticket.MoocNumber ?? registration.MoocNumber,
            NetWeight = ticket.NetWeight,
            Fields = new[]
            {
                Field("TicketNo", ticket.TicketNo),
                Field("VehiclePlate", FirstNonEmpty(ticket.VehiclePlate, registration.VehiclePlate)),
                Field("MoocNumber", FirstNonEmpty(ticket.MoocNumber, registration.MoocNumber)),
                Field("VehicleRegistrationNo", FirstNonEmpty(ticket.VehicleRegistrationNoSnapshot, vehicle?.VehicleRegistrationNo)),
                Field("MoocRegistrationNo", FirstNonEmpty(ticket.MoocRegistrationNoSnapshot, vehicle?.MoocRegistrationNo)),
                Field("CustomerName", FirstNonEmpty(ticket.CustomerName, registration.CustomerName)),
                Field("ProductName", FirstNonEmpty(ticket.ProductName, registration.ProductName)),
                Field("LotNo", registration.LotNo),
                Field("RepresentativeName", registration.RepresentativeName),
                Field("Notes", FirstNonEmpty(ticket.Notes, registration.Notes)),
                Field("Weight1Time", FormatDateTime(ticket.Weight1Time)),
                Field("Weight2Time", FormatDateTime(ticket.Weight2Time)),
                Field("EmptyWeight", FormatWeight(emptyWeight)),
                Field("GrossWeight", FormatWeight(grossWeight)),
                Field("NetWeight", FormatWeight(ticket.NetWeight)),
                Field("PrintedAt", FormatDateTime(printedAtLocal))
            }
        };
    }

    private static PrintFieldValue Field(string key, string? value) => new(key, value);
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    private static string? FormatDateTime(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    private static string? FormatDateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    private static string? FormatWeight(decimal? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : null;
}

public sealed class DeliveryTicketPrintComposer : IDeliveryTicketPrintComposer
{
    public DeliveryTicketPrintModel Compose(
        VehicleRegistration registration,
        DeliveryTicket deliveryTicket,
        WeighTicket? weighTicket,
        WeighingSessionLine? sessionLine,
        Vehicle? vehicle,
        DateTime printedAtLocal)
    {
        var vehicleLine = string.Join(" / ", new[] { FirstNonEmpty(weighTicket?.VehiclePlate, registration.VehiclePlate), FirstNonEmpty(weighTicket?.MoocNumber, registration.MoocNumber) }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
        var actualWeight = deliveryTicket.AllocatedWeight ?? sessionLine?.ActualAllocatedWeight ?? weighTicket?.NetWeight;
        var actualBagCount = (deliveryTicket.AllocatedBagCount ?? sessionLine?.ActualAllocatedBagCount)?.ToString(CultureInfo.InvariantCulture);

        return new DeliveryTicketPrintModel
        {
            DocumentId = deliveryTicket.Id,
            DisplayNumber = FirstNonEmpty(registration.CutOrderCode, deliveryTicket.DeliveryNo) ?? deliveryTicket.DeliveryNo,
            DeliveryNo = deliveryTicket.DeliveryNo,
            CutOrderCode = registration.CutOrderCode,
            OrderCode = FirstNonEmpty(registration.OrderCode, deliveryTicket.ErpVehicleRegistrationId, registration.ErpVehicleRegistrationId),
            ActualWeight = actualWeight,
            Fields = new[]
            {
                Field("DeliveryNo", FirstNonEmpty(registration.CutOrderCode, deliveryTicket.DeliveryNo)),
                Field("ReferenceCode", FirstNonEmpty(registration.OrderCode, deliveryTicket.ErpVehicleRegistrationId, registration.ErpVehicleRegistrationId)),
                Field("CustomerName", registration.CustomerName),
                Field("CustomerCode", registration.CustomerCode),
                Field("ProductName", registration.ProductName),
                Field("ConsumptionPlace", registration.ConsumptionPlace),
                Field("LoadingPlace", registration.LoadingPlace),
                Field("LotNo", registration.LotNo),
                Field("SealNo", registration.SealNo),
                Field("PlannedWeight", FormatWeight(registration.PlannedWeight)),
                Field("BagCount", registration.BagCount?.ToString(CultureInfo.InvariantCulture)),
                Field("ActualWeight", FormatWeight(actualWeight)),
                Field("ActualBagCount", actualBagCount),
                Field("VehicleLine", vehicleLine),
                Field("VehicleRegistrationNo", FirstNonEmpty(weighTicket?.VehicleRegistrationNoSnapshot, vehicle?.VehicleRegistrationNo)),
                Field("MoocRegistrationNo", FirstNonEmpty(weighTicket?.MoocRegistrationNoSnapshot, vehicle?.MoocRegistrationNo)),
                Field("Notes", FirstNonEmpty(deliveryTicket.Notes, registration.Notes)),
                Field("Weight1Time", FormatDateTime(weighTicket?.Weight1Time)),
                Field("Weight2Time", FormatDateTime(weighTicket?.Weight2Time)),
                Field("PrintedAt", FormatDateTime(printedAtLocal))
            }
        };
    }

    private static PrintFieldValue Field(string key, string? value) => new(key, value);
    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    private static string? FormatDateTime(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    private static string? FormatDateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    private static string? FormatWeight(decimal? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : null;
}
