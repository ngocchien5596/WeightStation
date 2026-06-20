namespace StationApp.Application.Interfaces;

public interface IErpCutOrderWriteBackService
{
    Task<ErpCutOrderWriteBackResult> UpdateTransportInfoAsync(ErpCutOrderWriteBackRequest request, CancellationToken ct);
    Task<ErpCutOrderSealWriteBackResult> UpdateSealNoAsync(ErpCutOrderSealWriteBackRequest request, CancellationToken ct);
    Task<ErpCutOrderNoteWriteBackResult> UpdateDescriptionAsync(ErpCutOrderNoteWriteBackRequest request, CancellationToken ct);
    Task<ErpCutOrderReceiverWriteBackResult> UpdateReceiverAsync(ErpCutOrderReceiverWriteBackRequest request, CancellationToken ct);
}

public sealed record ErpCutOrderWriteBackRequest(
    string ErpCutOrderId,
    string VehiclePlate,
    string? MoocNumber,
    string? UpdatedBy,
    DateTime UpdatedAt
);

public sealed record ErpCutOrderWriteBackResult(
    string ErpCutOrderId,
    int AffectedRows,
    string? PreviousVehiclePlate,
    string? PreviousMoocNumber,
    string? CurrentVehiclePlate,
    string? CurrentMoocNumber
);

public sealed record ErpCutOrderSealWriteBackRequest(
    string ErpCutOrderId,
    string? SealNo,
    string? UpdatedBy,
    DateTime UpdatedAt
);

public sealed record ErpCutOrderSealWriteBackResult(
    string ErpCutOrderId,
    int AffectedRows,
    string? PreviousSealNo,
    string? CurrentSealNo
);

public sealed record ErpCutOrderNoteWriteBackRequest(
    string ErpCutOrderId,
    string? Description,
    string? UpdatedBy,
    DateTime UpdatedAt
);

public sealed record ErpCutOrderNoteWriteBackResult(
    string ErpCutOrderId,
    int AffectedRows,
    string? PreviousDescription,
    string? CurrentDescription
);

public sealed record ErpCutOrderReceiverWriteBackRequest(
    string ErpCutOrderId,
    string? Receiver,
    string? UpdatedBy,
    DateTime UpdatedAt
);

public sealed record ErpCutOrderReceiverWriteBackResult(
    string ErpCutOrderId,
    int AffectedRows,
    string? PreviousReceiver,
    string? CurrentReceiver
);
