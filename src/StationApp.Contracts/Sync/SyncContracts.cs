namespace StationApp.Contracts.Sync;

public sealed class SyncWeighTicketRequest
{
    public Guid Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public sealed class SyncWeighTicketResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class InboundVehicleDto
{
    public string VehiclePlate { get; set; } = "";
    public string MoocNumber { get; set; } = "";
    public string? DriverName { get; set; }
    public string? TransportMethod { get; set; }
    public decimal? TtcpWeight { get; set; }
    public string? VehicleRegistrationNo { get; set; }
    public DateTime? VehicleRegistrationExpiryDate { get; set; }
    public string? MoocRegistrationNo { get; set; }
    public DateTime? MoocRegistrationExpiryDate { get; set; }
}

public sealed class InboundCustomerDto
{
    public string CustomerCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
}

public sealed class InboundProductDto
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
}

public sealed class InboundMasterDataResponse
{
    public bool Success { get; set; }
    public List<InboundVehicleDto> Vehicles { get; set; } = new();
    public List<InboundCustomerDto> Customers { get; set; } = new();
    public List<InboundProductDto> Products { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
