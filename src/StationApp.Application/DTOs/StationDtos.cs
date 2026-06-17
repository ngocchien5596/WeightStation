namespace StationApp.Application.DTOs;

public sealed record StationOptionDto(
    string StationCode,
    string StationName,
    bool IsDefault);

public sealed record StationManagementDto(
    Guid Id,
    string StationCode,
    string StationName,
    bool IsActive,
    int SortOrder,
    StationFeatureSetDto Features,
    StationOperationSettingsDto Settings,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public sealed record SaveStationRequest(
    Guid? StationId,
    string StationCode,
    string StationName,
    bool IsActive,
    int SortOrder,
    StationFeatureSetDto Features,
    StationOperationSettingsDto Settings);

public sealed record UserStationAssignmentDto(
    string StationCode,
    string StationName,
    bool IsAssigned,
    bool IsDefault);

public sealed record SaveUserStationAssignmentDto(
    string StationCode,
    bool IsAssigned,
    bool IsDefault);

public sealed record StationFeatureSetDto(
    bool ShowMenuDashboard,
    bool ShowMenuIncomingVehicleList,
    bool ShowMenuWeighing,
    bool ShowMenuCrusherWeighing,
    bool ShowMenuClayWeighing,
    bool ShowMenuExportWeighing,
    bool ShowMenuOutgoingVehicleList,
    bool ShowMenuExportReport,
    bool ShowMenuInboundReport,
    bool ShowMenuCrusherInboundReport,
    bool ShowMenuClayInboundReport,
    bool ShowDashboardInboundKpi,
    bool ShowDashboardOutboundKpi,
    string DefaultNavigationTarget)
{
    public static StationFeatureSetDto Defaults { get; } = new(
        ShowMenuDashboard: true,
        ShowMenuIncomingVehicleList: true,
        ShowMenuWeighing: true,
        ShowMenuCrusherWeighing: false,
        ShowMenuClayWeighing: false,
        ShowMenuExportWeighing: true,
        ShowMenuOutgoingVehicleList: true,
        ShowMenuExportReport: true,
        ShowMenuInboundReport: true,
        ShowMenuCrusherInboundReport: false,
        ShowMenuClayInboundReport: false,
        ShowDashboardInboundKpi: true,
        ShowDashboardOutboundKpi: true,
        DefaultNavigationTarget: "Dashboard");
}

public sealed record StationOperationSettingsDto(
    bool CrusherSingleWeighEnabled,
    string CrusherDefaultWeighMode,
    string CrusherDefaultProductCode,
    string CrusherDefaultCustomerCode,
    bool ClaySingleWeighEnabled,
    string ClayDefaultWeighMode,
    string ClayDefaultProductCode,
    string ClayDefaultCustomerCode)
{
    public static StationOperationSettingsDto Defaults { get; } = new(
        CrusherSingleWeighEnabled: false,
        CrusherDefaultWeighMode: "TWO_WEIGH",
        CrusherDefaultProductCode: "",
        CrusherDefaultCustomerCode: "",
        ClaySingleWeighEnabled: false,
        ClayDefaultWeighMode: "TWO_WEIGH",
        ClayDefaultProductCode: "",
        ClayDefaultCustomerCode: "");
}
