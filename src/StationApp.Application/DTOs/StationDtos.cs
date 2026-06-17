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
    StationFeatureSetDto Features);

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
    bool ShowMenuExportWeighing,
    bool ShowMenuOutgoingVehicleList,
    bool ShowMenuExportReport,
    bool ShowMenuInboundReport,
    bool ShowDashboardInboundKpi,
    bool ShowDashboardOutboundKpi,
    string DefaultNavigationTarget)
{
    public static StationFeatureSetDto Defaults { get; } = new(
        ShowMenuDashboard: true,
        ShowMenuIncomingVehicleList: true,
        ShowMenuWeighing: true,
        ShowMenuCrusherWeighing: false,
        ShowMenuExportWeighing: true,
        ShowMenuOutgoingVehicleList: true,
        ShowMenuExportReport: true,
        ShowMenuInboundReport: true,
        ShowDashboardInboundKpi: true,
        ShowDashboardOutboundKpi: true,
        DefaultNavigationTarget: "Dashboard");
}
