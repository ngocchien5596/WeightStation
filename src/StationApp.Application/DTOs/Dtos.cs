using StationApp.Domain.Enums;

namespace StationApp.Application.DTOs;

public sealed record CreateTicketRequest(
    string VehiclePlate,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? ErpCutOrderId = null,
    string? MoocNumber = null,
    string? DriverName = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    string? ProductType = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    string? Notes = null
);

public sealed record CaptureWeightRequest(
    Guid CutOrderId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode
);

public sealed record SplitOverweightTicketRequest(
    Guid CutOrderId,
    decimal Weight2,
    bool IsStable,
    WeightMode Mode
);


public sealed record CompleteTicketRequest(
    Guid CutOrderId
);

public sealed record CancelTicketRequest(
    Guid CutOrderId
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
    string ToleranceKgPerBag,
    string SyncIntervalSeconds,
    string RegistrationInboundPollSeconds,
    string OverweightSplitStepWeight,
    string CentralApiUrl,
    string CentralApiKey,
    string LocalDatabaseBackupDirectory,
    string LocalDatabaseBackupTime
);

public sealed record ExternalDatacanRecordDto(
    string? TicketNo,
    string? VehiclePlate,
    string? GroupName,
    string? CustomerName,
    string? ProductName,
    DateTime? Weight1Time,
    DateTime? Weight2Time,
    decimal? Weight1,
    decimal? Weight2,
    decimal? NetWeight,
    string? OperatorName
);

public sealed record ExternalDatacanQueryResult(
    IReadOnlyList<ExternalDatacanRecordDto> Records,
    bool HasNextPage
);

public sealed record UpdateCameraSettingsRequest(
    bool Camera1Enabled,
    string Camera1Name,
    string Camera1RtspUrl,
    string Camera1PreviewRtspUrl,
    bool Camera2Enabled,
    string Camera2Name,
    string Camera2RtspUrl,
    string Camera2PreviewRtspUrl,
    bool CameraC6_1Enabled,
    string CameraC6_1Name,
    string CameraC6_1RtspUrl,
    string CameraC6_1PreviewRtspUrl,
    bool CameraC6_2Enabled,
    string CameraC6_2Name,
    string CameraC6_2RtspUrl,
    string CameraC6_2PreviewRtspUrl,
    string CameraPreviewDefault,
    string CameraCaptureTimeoutMs,
    string CameraCaptureJpegQuality,
    string CameraCaptureWarmupFrames,
    bool CameraCrusher_1Enabled = false,
    string CameraCrusher_1Name = "",
    string CameraCrusher_1RtspUrl = "",
    string CameraCrusher_1PreviewRtspUrl = "",
    bool CameraCrusher_2Enabled = false,
    string CameraCrusher_2Name = "",
    string CameraCrusher_2RtspUrl = "",
    string CameraCrusher_2PreviewRtspUrl = "",
    bool CameraClay_1Enabled = false,
    string CameraClay_1Name = "",
    string CameraClay_1RtspUrl = "",
    string CameraClay_1PreviewRtspUrl = "",
    bool CameraClay_2Enabled = false,
    string CameraClay_2Name = "",
    string CameraClay_2RtspUrl = "",
    string CameraClay_2PreviewRtspUrl = ""
);

public sealed record CameraEndpointSettings(
    string CameraCode,
    string DisplayName,
    string MainRtspUrl,
    string PreviewRtspUrl,
    bool IsEnabled
)
{
    public string CaptureRtspUrl => MainRtspUrl;
    public string EffectivePreviewRtspUrl => string.IsNullOrWhiteSpace(PreviewRtspUrl) ? MainRtspUrl : PreviewRtspUrl;
}

public sealed record CameraSystemSettings(
    CameraEndpointSettings Camera1,
    CameraEndpointSettings Camera2,
    string PreviewDefaultCameraCode,
    int CaptureTimeoutMs,
    int CaptureJpegQuality,
    int CaptureMaxDimension,
    int CaptureWarmupFrames
)
{
    public IReadOnlyList<CameraEndpointSettings> EnabledCameras =>
        new[] { Camera1, Camera2 }.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.MainRtspUrl)).ToList();
}

public sealed record CameraCaptureImageResult(
    string CameraCode,
    string CameraName,
    string RtspUrlSnapshot,
    string ImageFormat,
    byte[] ImageBytes,
    DateTime CapturedAt,
    string? ErrorMessage = null)
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage) && ImageBytes.Length > 0;
}

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
    string? ErpCutOrderId,
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
    Guid CutOrderId,
    string? TicketNo,
    string? ErpCutOrderId,
    string VehiclePlate,
    string? CustomerName,
    string? ProductName,
    CutOrderStatus CutOrderStatus,
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

public sealed record CreateCutOrderRequest(
    string VehiclePlate,
    CutOrderSource CutOrderSource,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? ErpCutOrderId = null,
    string? ErpRegistrationCode = null,
    string? MoocNumber = null,
    string? ReceiverName = null,
    string? ReceiverIdNo = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    string? ProductType = null,
    string? OrderCode = null,
    string? LotNo = null,
    string? RepresentativeName = null,
    string? Market = null,
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
    string? ErpCutOrderId,
    string? VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    string? ProductCode,
    string? ProductName
);

public sealed record OutgoingVehicleListFilter(
    string? SessionNo,
    string? ErpCutOrderId,
    string? VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    DateTime? CompletedDate,
    OutgoingFlowType FlowType = OutgoingFlowType.All
);

public sealed record IncomingVehicleListItem(
    Guid CutOrderId,
    string? ErpCutOrderId,
    string? ErpRegistrationCode,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    string? ReceiverName,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? BagCount,
    CutOrderStatus CutOrderStatus,
    TransportMethod? TransportMethod,
    DateTime CreatedAt,
    string? ProductType = null,
    decimal? CarryForwardWeight1 = null,
    DateTime? CarryForwardWeight1Time = null,
    string? SuggestedSessionNo = null,
    string? ConsumptionPlace = null,
    string? Market = null,
    bool IsExportScale = false,
    decimal? ExportAccumulatedWeight = null,
    decimal? ExportRemainingWeight = null,
    int ExportTripCount = 0
);

public sealed record OutgoingVehicleListItem(
    Guid CutOrderId,
    Guid? WeighingSessionId,
    string? SessionNo,
    string? ErpCutOrderId,
    TransactionType TransactionType,
    string VehiclePlate,
    string? MoocNumber,
    TransportMethod? TransportMethod,
    string? ReceiverName,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    string? ProductType,
    string? DriverName,
    decimal? PlannedWeight,
    int? PlannedBagCount,
    decimal? Weight1,
    decimal? ActualWeightKg,
    int? ActualBagCount,
    decimal? TotalWeightKg,
    int? TotalBagCount,
    DateTime? WeighDate,
    string? WeighUser,
    string? WeighTicketNo,
    string? DeliveryNo,
    DateTime? CompletedAt,
    bool HasPrintedWeighTicket,
    bool HasPrintedDeliveryTicket,
    bool UseActualWeightForBaggedCutOrders,
    bool ErpExportCompleted,
    bool IsNoLoad,
    bool IsExportScale,
    bool IsPortTransfer,
    bool HighlightAsSplitOverweight
);

public sealed record ExportScaleCutOrderFilter(
    string? ErpCutOrderId,
    string? VehiclePlate,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    bool IncludeErpCompletedFinalized = false
);

public sealed record ExportScaleCutOrderListItem(
    Guid CutOrderId,
    string? ErpCutOrderId,
    string VehiclePlate,
    string? MoocNumber,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? PlannedBagCountDisplay,
    decimal AccumulatedWeight,
    int AccumulatedBagCountDisplay,
    decimal RemainingWeight,
    int RemainingBagCountDisplay,
    decimal? TareWeightKg,
    decimal? BagWeightKg,
    int TripCount,
    DateTime? LastTripAt,
    bool IsFinalized,
    bool ErpExportCompleted,
    CutOrderStatus CutOrderStatus,
    ProcessingStage ProcessingStage,
    string? Notes,
    bool IsTemporaryExport = false,
    string? TemporaryExportDisplayCode = null)
{
    public string DisplayCutOrderCode => IsTemporaryExport
        ? TemporaryExportDisplayCode ?? ErpCutOrderId ?? string.Empty
        : ErpCutOrderId ?? string.Empty;
}

public sealed record TemporaryExportCutOrderOption(
    Guid CutOrderId,
    string DisplayCode,
    string? CustomerCode,
    string? CustomerName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    decimal AccumulatedWeight,
    int TripCount,
    DateTime? LastTripAt,
    string? Notes,
    int MatchScore
);

public sealed record ExportVehicleTripListItem(
    Guid SessionId,
    Guid SessionLineId,
    string SessionNo,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    decimal? Weight1,
    decimal? Weight2,
    decimal? NetWeight,
    decimal? ActualAllocatedWeight,
    int? BagCountDisplay,
    DateTime? Weight1Time,
    DateTime? Weight2Time,
    WeighingSessionStatus SessionStatus,
    string? WeighTicketNo,
    string? DeliveryNo,
    bool HasPrintedWeighTicket,
    bool HasPrintedDeliveryTicket,
    string? Note = null)
{
    public bool IsReturnedBrokenTrip { get; set; }
    public bool CanToggleReturnedBrokenTrip { get; set; }
}

public sealed record TransitionToExportScaleRequest(Guid CutOrderId);

public sealed record CreateTemporaryExportCutOrderRequest(
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    string? ProductType = null,
    decimal? PlannedWeight = null,
    int? BagCount = null,
    decimal? TareWeightKg = null,
    decimal? BagWeightKg = null,
    string? Notes = null
);

public sealed record MapTemporaryExportCutOrderRequest(
    Guid TemporaryCutOrderId,
    Guid RealCutOrderId
);

public sealed record CreateExportVehicleSessionRequest(
    Guid CutOrderId,
    string VehiclePlate,
    string? MoocNumber,
    string? DriverName,
    decimal? TtcpWeight,
    string? VehicleRegistrationNo,
    DateTime? VehicleRegistrationExpiryDate,
    string? MoocRegistrationNo,
    DateTime? MoocRegistrationExpiryDate
);

public sealed record CreateExportVehicleSessionResult(
    Guid SessionId,
    string SessionNo
);

public sealed record TransferExportVehicleTripRequest(
    Guid SessionId,
    Guid TargetCutOrderId
);

public sealed record FinalizeExportCutOrderRequest(Guid CutOrderId);

public sealed record ConfirmEnterWeighingRequest(
    Guid CutOrderId
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
    string? ProductType = null,
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
    Guid CutOrderId,
    string VehiclePlate,
    TransactionType TransactionType,
    TransportMethod? TransportMethod = null,
    string? MoocNumber = null,
    string? ReceiverName = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? ProductCode = null,
    string? ProductName = null,
    string? ProductType = null,
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
    IReadOnlyList<Guid> CutOrderIds,
    Guid? PrimaryCutOrderId = null,
    bool ApplyCarryForwardWeight1 = false
);

public sealed record AppendCutOrdersToWeighingSessionRequest(
    Guid SessionId,
    IReadOnlyList<Guid> CutOrderIds
);

public sealed record MarkRegistrationsNoLoadRequest(
    IReadOnlyList<Guid> CutOrderIds,
    Guid? PrimaryCutOrderId = null
);

public sealed record CreateWeighingSessionResult(
    Guid SessionId
);

public sealed record CaptureSessionWeightRequest(
    Guid SessionId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode,
    bool BypassTolerance = false,
    int? ConfirmedBagCount = null,
    int? SystemCalculatedBagCount = null,
    bool IsReturnedBrokenTrip = false,
    string? Note = null
);

public sealed record AllocateWeighingSessionLineRequest(
    Guid SessionLineId,
    decimal? ActualAllocatedWeight,
    int? ActualAllocatedBagCount,
    bool IsPriority = false
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
    bool UseActualWeightForBaggedCutOrders,
    bool IsNoLoad,
    bool IsPortTransfer,
    bool AllDeliveryTicketsPrinted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CustomerSummary = null,
    string? ProductSummary = null
);

public sealed record InternalVehicleOptionDto(
    Guid VehicleId,
    string VehiclePlate,
    string? DriverName,
    decimal? StandardTareWeight,
    string? StandardTareSource
);

public sealed record CrusherWeighingSessionListItem(
    Guid SessionId,
    string SessionNo,
    string VehiclePlate,
    string? DriverName,
    decimal? Weight1,
    DateTime? Weight1Time,
    decimal? Weight2,
    DateTime? Weight2Time,
    decimal? NetWeight,
    string WeighingMode,
    decimal? StandardTareWeightSnapshot,
    string? StandardTareSourceSnapshot,
    WeighingSessionStatus SessionStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Crusher Weighing: Product and Customer Information
    string? ProductCode,
    string? ProductName,
    string? CustomerCode,
    string? CustomerName
);

public sealed record WeighingSessionLineItem(
    Guid SessionLineId,
    Guid CutOrderId,
    int SequenceNo,
    string? ErpCutOrderId,
    string? CustomerName,
    string? DistributorName,
    string? ProductCode,
    string? ProductName,
    decimal? PlannedWeight,
    int? PlannedBagCount,
    decimal? ActualAllocatedWeight,
    int? ActualAllocatedBagCount,
    int? BagCountDisplay,
    WeighingSessionLineStatus LineStatus,
    bool HasPrintedDeliveryTicket,
    string? ProductType = null,
    string? Notes = null,
    bool IsPortTransfer = false
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
    string? ErpCutOrderId,
    string? CustomerName,
    string? ProductName,
    byte SplitSequence,
    decimal AllocatedWeight,
    int? AllocatedBagCount,
    decimal? Weight1,
    decimal? Weight2
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
    string RegistrationSummary,
    decimal? PlannedWeight,
    decimal? Weight1,
    decimal? Weight2,
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
    ProductName = 6,
    CustomerCode = 7
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
    string? ProductName = null,
    string? ProductType = null
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
    string? ProductType,
    string Source
);
