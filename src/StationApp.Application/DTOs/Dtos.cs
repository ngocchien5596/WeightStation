using StationApp.Domain.Enums;

namespace StationApp.Application.DTOs;

public sealed record CreateTicketRequest(
    string VehiclePlate,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? ErpVehicleRegistrationId = null,
    string? MoocNumber = null,
    string? DriverName = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    string? Notes = null
);

public sealed record CaptureWeightRequest(
    Guid RegistrationId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode
);

public sealed record SplitOverweightTicketRequest(
    Guid RegistrationId,
    decimal Weight2,
    bool IsStable,
    WeightMode Mode
);


public sealed record CompleteTicketRequest(
    Guid RegistrationId
);

public sealed record CancelTicketRequest(
    Guid RegistrationId
);

public sealed record SearchUsersRequest(
    string? Username,
    string? DisplayName,
    string? RoleCode,
    bool? IsActive
);

public sealed record CreateUserAccountRequest(
    string Username,
    string DisplayName,
    string RoleCode,
    string Password,
    string ConfirmPassword,
    bool IsActive
);

public sealed record UpdateUserAccountRequest(
    Guid UserId,
    string DisplayName,
    string RoleCode,
    bool IsActive
);

public sealed record ResetUserPasswordRequest(
    Guid UserId,
    string NewPassword,
    string ConfirmPassword
);

public sealed record SetUserActiveStatusRequest(
    Guid UserId,
    bool IsActive
);

public sealed record LoginRequest(
    string Username,
    string Password
);

public sealed record CurrentUserSessionDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string RoleCode,
    bool IsAuthenticated
);

public sealed record UpdateSystemSettingsRequest(
    string StationCode,
    string TicketPrefix,
    string DeliveryPrefix,
    string ToleranceKg,
    string SyncIntervalSeconds,
    string RegistrationInboundPollSeconds,
    string OverweightSplitStepWeight,
    bool PilotModeEnabled
);

public sealed record UpdateScaleDeviceSettingsRequest(
    string ComPort,
    string Baudrate,
    string Parity,
    string DataBits,
    string StopBits,
    string ParserType,
    string FrameEndChar,
    string StableCycles,
    string WeightSubstringStart,
    string WeightSubstringLength
);

public sealed record UserListItemDto(
    Guid Id,
    string Username,
    string DisplayName,
    string RoleCode,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public sealed record TicketListItemDto(
    Guid Id,
    string TicketNo,
    string VehiclePlate,
    TicketStatus Status,
    SyncStatus SyncStatus,
    TransactionType TransactionType,
    decimal? Weight1,
    decimal? Weight2,
    decimal? NetWeight,
    DateTime CreatedAt
);

public sealed record TicketDetailDto(
    Guid Id,
    string TicketNo,
    string? ErpVehicleRegistrationId,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    string? CustomerCode,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? BagCount,
    string? Notes,
    TransactionType TransactionType,
    TransportMethod? TransportMethod,
    bool IsCancelled,
    TicketStatus Status,
    SyncStatus SyncStatus,
    decimal? Weight1,
    string? Weight1User,
    DateTime? Weight1Time,
    WeightMode? Weight1Mode,
    bool? Weight1IsStable,
    decimal? Weight2,
    string? Weight2User,
    DateTime? Weight2Time,
    WeightMode? Weight2Mode,
    bool? Weight2IsStable,
    decimal? NetWeight,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record WeightViewListItem(
    Guid RegistrationId,
    string? TicketNo,
    string? ErpVehicleRegistrationId,
    string VehiclePlate,
    string? CustomerName,
    string? ProductName,
    RegistrationStatus RegistrationStatus,
    decimal? Weight1,
    decimal? Weight2,
    int? BagCount,
    decimal? PlannedWeight,
    decimal? NetWeight,
    DateTime? WeighDate,
    string? WeighUser,
    string? DeliveryNo,
    string? Notes,
    bool HasOverweightCase,
    TransactionType TransactionType,
    TransportMethod? TransportMethod
);

public sealed record RelatedDocumentListItem(
    string DocumentType,
    string? TicketNo,
    string? DeliveryNo,
    string RecordRole,
    byte? SplitSequence,
    decimal? Weight1,
    decimal? Weight2,
    decimal? NetWeight,
    DateTime CreatedAt
);

public sealed record CreateVehicleRegistrationRequest(
    string VehiclePlate,
    RegistrationSource RegistrationSource,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? ErpVehicleRegistrationId = null,
    string? MoocNumber = null,
    string? ReceiverName = null,
    string? ReceiverIdNo = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    string? CutOrderCode = null,
    string? OrderCode = null,
    string? LotNo = null,
    string? RepresentativeName = null,
    string? ConsumptionPlace = null,
    string? LoadingPlace = null,
    string? SealNo = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    string? Notes = null
);

public sealed class OperationResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public List<OperationWarning> Warnings { get; init; } = new();

    public static OperationResult<T> Ok(T data, List<OperationWarning>? warnings = null)
        => new() { Success = true, Data = data, Warnings = warnings ?? new() };

    public static OperationResult<T> Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}

public sealed record OperationWarning(string Code, string Message);

public sealed record IncomingVehicleListFilter(
    string? ErpVehicleRegistrationId,
    string? VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    string? ProductCode,
    string? ProductName
);

public sealed record OutgoingVehicleListFilter(
    string? ErpVehicleRegistrationId,
    string? VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName
);

public sealed record IncomingVehicleListItem(
    Guid RegistrationId,
    string? ErpVehicleRegistrationId,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? BagCount,
    RegistrationStatus RegistrationStatus,
    TransportMethod? TransportMethod,
    DateTime CreatedAt
);

public sealed record OutgoingVehicleListItem(
    Guid RegistrationId,
    string? ErpVehicleRegistrationId,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    string? ProductName,
    decimal? NetWeight,
    DateTime? CompletedAt,
    TransportMethod? TransportMethod,
    bool AllWeighTicketsPrinted,
    bool AllDeliveryTicketsPrinted
);

public sealed record ConfirmEnterWeighingRequest(
    Guid RegistrationId
);

public sealed record CreateInboundRegistrationRequest(
    string VehiclePlate,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? MoocNumber = null,
    string? ReceiverName = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    string? Notes = null,
    decimal? TtcpWeight = null,
    string? VehicleRegistrationNo = null,
    DateTime? VehicleRegistrationExpiryDate = null,
    string? MoocRegistrationNo = null,
    DateTime? MoocRegistrationExpiryDate = null
);

public sealed record UpdateIncomingRegistrationRequest(
    Guid RegistrationId,
    string VehiclePlate,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? MoocNumber = null,
    string? ReceiverName = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    string? Notes = null,
    bool IsCancelled = false,
    decimal? TtcpWeight = null,
    string? VehicleRegistrationNo = null,
    DateTime? VehicleRegistrationExpiryDate = null,
    string? MoocRegistrationNo = null,
    DateTime? MoocRegistrationExpiryDate = null
);

public sealed record CreateWeighingSessionRequest(
    IReadOnlyList<Guid> RegistrationIds,
    Guid? PrimaryRegistrationId = null
);

public sealed record MarkRegistrationsNoLoadRequest(
    IReadOnlyList<Guid> RegistrationIds,
    Guid? PrimaryRegistrationId = null
);

public sealed record CreateWeighingSessionResult(
    Guid SessionId
);

public sealed record CaptureSessionWeightRequest(
    Guid SessionId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode
);

public sealed record AllocateWeighingSessionLineRequest(
    Guid SessionLineId,
    decimal? ActualAllocatedWeight,
    int? ActualAllocatedBagCount
);

public sealed record AllocateWeighingSessionRequest(
    Guid SessionId,
    IReadOnlyList<AllocateWeighingSessionLineRequest> Lines
);

public sealed record CancelWeighingSessionRequest(
    Guid SessionId
);

public sealed record MarkWeighingSessionNoLoadRequest(
    Guid SessionId
);

public sealed record WeighingSessionListItem(
    Guid SessionId,
    string SessionNo,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    decimal? Weight1,
    decimal? Weight2,
    decimal? NetWeight,
    decimal? Ttcp10WeightSnapshot,
    bool IsOverweight,
    decimal OverweightAmount,
    OverweightResolutionStatus OverweightResolutionStatus,
    WeighingSessionStatus SessionStatus,
    int LineCount,
    bool HasPrintedMasterWeighTicket,
    bool AllDeliveryTicketsPrinted,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record WeighingSessionLineItem(
    Guid SessionLineId,
    Guid VehicleRegistrationId,
    int SequenceNo,
    string? ErpVehicleRegistrationId,
    string? CustomerName,
    string? DistributorName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? PlannedBagCount,
    decimal? ActualAllocatedWeight,
    int? ActualAllocatedBagCount,
    WeighingSessionLineStatus LineStatus,
    bool HasPrintedDeliveryTicket
);

public sealed record OverweightSplitPreviewGroupItem(
    Guid SplitGroupId,
    byte SplitSequence,
    decimal GroupWeight,
    int DeliveryTicketCount
);

public sealed record PreviewWeighingSessionOverweightSplitRequest(
    Guid SessionId,
    decimal? FirstSplitNetWeight = null,
    bool IsManualOverride = false
);

public sealed record ResolveWeighingSessionOverweightSplitRequest(
    Guid SessionId,
    decimal? FirstSplitNetWeight = null,
    bool IsManualOverride = false
);

public sealed record OverweightSplitPreviewLineItem(
    Guid SessionLineId,
    int SequenceNo,
    string? ErpVehicleRegistrationId,
    string? CustomerName,
    string? ProductName,
    byte SplitSequence,
    decimal AllocatedWeight,
    int? AllocatedBagCount
);

public sealed record OverweightSplitPreviewDto(
    Guid SessionId,
    decimal Ttcp10WeightSnapshot,
    decimal NetWeight,
    decimal OverweightAmount,
    decimal OverweightSplitStepWeight,
    decimal SplitTicket1NetWeight,
    decimal SplitTicket2NetWeight,
    decimal? RandomSplitFactor,
    bool IsManualOverride,
    IReadOnlyList<OverweightSplitPreviewGroupItem> Groups,
    IReadOnlyList<OverweightSplitPreviewLineItem> Lines
);

public sealed record OutgoingSessionListItem(
    Guid SessionId,
    string SessionNo,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    decimal? NetWeight,
    int LineCount,
    bool HasPrintedMasterWeighTicket,
    bool AllDeliveryTicketsPrinted,
    DateTime? CompletedAt
);

public enum AutocompleteFieldType
{
    Vehicle = 1,
    Mooc = 2,
    Driver = 3,
    Customer = 4,
    ProductCode = 5,
    ProductName = 6
}

public sealed record AutocompleteQuery(
    AutocompleteFieldType FieldType,
    string SearchText,
    int Limit = 10
);

public sealed record AutocompletePayload(
    string? VehiclePlate = null,
    string? MoocNumber = null,
    string? DriverName = null,
    decimal? TtcpWeight = null,
    string? VehicleRegistrationNo = null,
    DateTime? VehicleRegistrationExpiryDate = null,
    string? MoocRegistrationNo = null,
    DateTime? MoocRegistrationExpiryDate = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null
);

public sealed record AutocompleteItem(
    string Value,
    string DisplayText,
    string? SecondaryText,
    AutocompleteFieldType FieldType,
    AutocompletePayload? Payload = null
);

public sealed record VehicleAutocompleteSource(
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    decimal? TtcpWeight,
    string? VehicleRegistrationNo,
    DateTime? VehicleRegistrationExpiryDate,
    string? MoocRegistrationNo,
    DateTime? MoocRegistrationExpiryDate,
    string Source
);

public sealed record DriverAutocompleteSource(
    string DriverName,
    string? VehiclePlate,
    string? MoocNumber,
    string Source
);

public sealed record CustomerAutocompleteSource(
    string? CustomerCode,
    string CustomerName,
    string Source
);

public sealed record ProductAutocompleteSource(
    string ProductCode,
    string ProductName,
    string Source
);
