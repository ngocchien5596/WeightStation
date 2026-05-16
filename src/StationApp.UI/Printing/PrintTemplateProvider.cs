using System.Globalization;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrintTemplateProvider : IPrintTemplateProvider
{
    private readonly IAppConfigRepository _appConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PrintTemplateProvider(IAppConfigRepository appConfigRepository, IUnitOfWork unitOfWork)
    {
        _appConfigRepository = appConfigRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, CancellationToken ct)
    {
        return kind switch
        {
            PrintDocumentKind.WeighTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "WeighTicketPrintTemplate",
                DefaultOffsetXmm = await GetOffsetAsync("print_weigh_offset_x_mm", ct),
                DefaultOffsetYmm = await GetOffsetAsync("print_weigh_offset_y_mm", ct),
                Fields = await LoadFieldLayoutAsync(kind, WeighTicketFields, ct)
            },
            PrintDocumentKind.DeliveryTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "DeliveryTicketPrintTemplate",
                DefaultOffsetXmm = await GetOffsetAsync("print_delivery_offset_x_mm", ct),
                DefaultOffsetYmm = await GetOffsetAsync("print_delivery_offset_y_mm", ct),
                Fields = await LoadFieldLayoutAsync(kind, DeliveryTicketFields, ct)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public async Task SaveLayoutAsync(
        PrintDocumentKind kind,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct)
    {
        await _appConfigRepository.SetValueAsync(GetGlobalOffsetKey(kind, isX: true), offsetXmm.ToString(CultureInfo.InvariantCulture), ct);
        await _appConfigRepository.SetValueAsync(GetGlobalOffsetKey(kind, isX: false), offsetYmm.ToString(CultureInfo.InvariantCulture), ct);

        foreach (var field in fieldPositions)
        {
            await _appConfigRepository.SetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: true), field.X.ToString(CultureInfo.InvariantCulture), ct);
            await _appConfigRepository.SetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: false), field.Y.ToString(CultureInfo.InvariantCulture), ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<double> GetOffsetAsync(string key, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(key, ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    private async Task<IReadOnlyList<PrintFieldDefinition>> LoadFieldLayoutAsync(
        PrintDocumentKind kind,
        IReadOnlyList<PrintFieldDefinition> defaultFields,
        CancellationToken ct)
    {
        var fields = new List<PrintFieldDefinition>(defaultFields.Count);
        foreach (var field in defaultFields)
        {
            var xRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: true), ct);
            var yRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: false), ct);

            var x = double.TryParse(xRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : field.X;
            var y = double.TryParse(yRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : field.Y;

            fields.Add(field with { X = x, Y = y });
        }

        return fields;
    }

    private static string GetGlobalOffsetKey(PrintDocumentKind kind, bool isX)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => isX ? "print_weigh_offset_x_mm" : "print_weigh_offset_y_mm",
            PrintDocumentKind.DeliveryTicket => isX ? "print_delivery_offset_x_mm" : "print_delivery_offset_y_mm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetFieldOffsetKey(PrintDocumentKind kind, string fieldKey, bool isX)
        => $"print_{GetTemplatePrefix(kind)}_field_{fieldKey}_{(isX ? "x" : "y")}_mm";

    private static string GetTemplatePrefix(PrintDocumentKind kind)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => "weigh",
            PrintDocumentKind.DeliveryTicket => "delivery",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static readonly IReadOnlyList<PrintFieldDefinition> WeighTicketFields =
    [
        new("TicketNo", 150, 23, 38, PrintFieldAlignment.Center, 12, PrintFieldWeight.Bold),
        new("VehiclePlate", 32, 42, 136, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("VehicleRegistrationNo", 32, 55, 45, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("MoocRegistrationNo", 118, 55, 50, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("CustomerName", 32, 71, 150, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("ProductName", 32, 84, 150, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("LotNo", 32, 97, 65, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("RepresentativeName", 118, 97, 64, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("Notes", 32, 110, 150, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("Weight1DateTime", 143, 55, 44, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("Weight2DateTime", 143, 68, 44, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("EmptyWeight", 160, 84, 23, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("GrossWeight", 160, 97, 23, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("NetWeight", 160, 110, 23, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("PrintedAt", 149, 154, 38, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("PrintedBy", 32, 154, 70, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold)
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> DeliveryTicketFields =
    [
        new("DeliveryNo", 141, 18, 42, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("ReferenceCode", 141, 31, 42, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("CustomerName", 53, 60, 132, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("ConsumptionPlace", 53, 74, 65, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("LoadingPlace", 53, 88, 65, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("CustomerCode", 156, 88, 28, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("ProductName", 19, 120, 38, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PlannedWeight", 67, 121, 12, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("BagCount", 83, 121, 12, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("ActualWeight", 107, 121, 12, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("ActualBagCount", 123, 121, 12, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("LotNo", 142, 121, 18, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("VehicleLine", 168, 118, 21, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("SealNo", 44, 138, 45, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("Weight1Hour", 111, 152, 8, PrintFieldAlignment.Center, 12, PrintFieldWeight.Normal),
        new("Weight1Minute", 131, 152, 8, PrintFieldAlignment.Center, 12, PrintFieldWeight.Normal),
        new("Weight1Date", 156, 152, 28, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("Weight2Hour", 111, 167, 8, PrintFieldAlignment.Center, 12, PrintFieldWeight.Normal),
        new("Weight2Minute", 131, 167, 8, PrintFieldAlignment.Center, 12, PrintFieldWeight.Normal),
        new("Weight2Date", 156, 167, 28, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal),
        new("Notes", 18, 181, 166, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PrintedBy", 18, 196, 80, PrintFieldAlignment.Left, 12, PrintFieldWeight.Normal)
    ];
}
