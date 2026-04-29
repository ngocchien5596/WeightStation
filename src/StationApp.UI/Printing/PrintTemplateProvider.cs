using StationApp.Application.Interfaces;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrintTemplateProvider : IPrintTemplateProvider
{
    private readonly IAppConfigRepository _appConfigRepository;

    public PrintTemplateProvider(IAppConfigRepository appConfigRepository)
    {
        _appConfigRepository = appConfigRepository;
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
                Fields = WeighTicketFields
            },
            PrintDocumentKind.DeliveryTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "DeliveryTicketPrintTemplate",
                DefaultOffsetXmm = await GetOffsetAsync("print_delivery_offset_x_mm", ct),
                DefaultOffsetYmm = await GetOffsetAsync("print_delivery_offset_y_mm", ct),
                Fields = DeliveryTicketFields
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private async Task<double> GetOffsetAsync(string key, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(key, ct);
        return double.TryParse(raw, out var value) ? value : 0d;
    }

    private static readonly IReadOnlyList<PrintFieldDefinition> WeighTicketFields =
    [
        new("TicketNo", 150, 23, 38, PrintFieldAlignment.Center, 12, PrintFieldWeight.Bold),
        new("VehiclePlate", 32, 42, 45, PrintFieldAlignment.Left, 12, PrintFieldWeight.Bold),
        new("MoocNumber", 118, 42, 50, PrintFieldAlignment.Left, 11, PrintFieldWeight.SemiBold),
        new("VehicleRegistrationNo", 32, 55, 45, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("MoocRegistrationNo", 118, 55, 50, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("CustomerName", 32, 71, 150, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("ProductName", 32, 84, 150, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("LotNo", 32, 97, 65, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("RepresentativeName", 118, 97, 64, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("Notes", 32, 110, 150, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("Weight1Time", 32, 136, 52, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("Weight2Time", 95, 136, 52, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("EmptyWeight", 158, 136, 28, PrintFieldAlignment.Right, 11, PrintFieldWeight.Bold),
        new("GrossWeight", 158, 149, 28, PrintFieldAlignment.Right, 11, PrintFieldWeight.Bold),
        new("NetWeight", 158, 162, 28, PrintFieldAlignment.Right, 12, PrintFieldWeight.Bold),
        new("PrintedAt", 132, 265, 54, PrintFieldAlignment.Right, 9, PrintFieldWeight.Normal)
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> DeliveryTicketFields =
    [
        new("DeliveryNo", 150, 23, 38, PrintFieldAlignment.Center, 12, PrintFieldWeight.Bold),
        new("ReferenceCode", 32, 39, 70, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("CustomerName", 32, 54, 150, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("CustomerCode", 32, 67, 50, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("ProductName", 90, 67, 92, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("ConsumptionPlace", 32, 82, 150, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("LoadingPlace", 32, 95, 150, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("LotNo", 32, 110, 58, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("SealNo", 118, 110, 64, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("PlannedWeight", 32, 129, 28, PrintFieldAlignment.Right, 10, PrintFieldWeight.SemiBold),
        new("BagCount", 67, 129, 22, PrintFieldAlignment.Right, 10, PrintFieldWeight.SemiBold),
        new("ActualWeight", 109, 129, 28, PrintFieldAlignment.Right, 10, PrintFieldWeight.Bold),
        new("ActualBagCount", 144, 129, 22, PrintFieldAlignment.Right, 10, PrintFieldWeight.SemiBold),
        new("VehicleLine", 32, 146, 150, PrintFieldAlignment.Left, 10, PrintFieldWeight.SemiBold),
        new("VehicleRegistrationNo", 32, 159, 62, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal),
        new("MoocRegistrationNo", 118, 159, 64, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal),
        new("Notes", 32, 177, 150, PrintFieldAlignment.Left, 9, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("Weight1Time", 32, 197, 64, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("Weight2Time", 118, 197, 64, PrintFieldAlignment.Left, 10, PrintFieldWeight.Normal),
        new("PrintedAt", 132, 265, 54, PrintFieldAlignment.Right, 9, PrintFieldWeight.Normal)
    ];
}
