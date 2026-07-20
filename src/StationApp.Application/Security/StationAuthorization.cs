using StationApp.Application.Interfaces;

namespace StationApp.Application.Security;

public static class StationRoles
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Operator = "OPERATOR";

    public static readonly IReadOnlySet<string> SupportedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        Manager,
        Operator
    };
}

public static class StationAuthorization
{
    public static bool IsAdmin(string? roleCode)
        => string.Equals(roleCode, StationRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsOperator(string? roleCode)
        => string.Equals(roleCode, StationRoles.Operator, StringComparison.OrdinalIgnoreCase);

    public static bool IsManager(string? roleCode)
        => string.Equals(roleCode, StationRoles.Manager, StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminOrManager(string? roleCode) => IsAdmin(roleCode) || IsManager(roleCode);
    private static bool IsAnyApplicationRole(string? roleCode) => IsAdmin(roleCode) || IsManager(roleCode) || IsOperator(roleCode);

    public static bool CanUseManualWeighing(string? roleCode) => IsAnyApplicationRole(roleCode);
    public static bool CanDeleteWeight2(string? roleCode) => IsAdminOrManager(roleCode);
    public static bool CanManageAccounts(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManageSystemSettings(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManageDeviceConfiguration(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManagePrintLayout(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewDiagnostics(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewSettingsAdministration(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewMasterData(string? roleCode) => CanViewMasterData(roleCode, null);
    public static bool CanViewMasterData(string? roleCode, string? stationCode) => CanManageMasterData(roleCode, stationCode);
    public static bool CanManageMasterData(string? roleCode, string? stationCode)
    {
        if (IsAdminOrManager(roleCode))
        {
            return true;
        }

        return IsOperator(roleCode) && IsOperatorMasterDataStation(stationCode);
    }

    public static bool CanViewOperationalScreens(string? roleCode) => IsAnyApplicationRole(roleCode);
    public static bool CanViewReports(string? roleCode) => IsAnyApplicationRole(roleCode);
    public static bool CanViewEditHistory(string? roleCode) => IsAdminOrManager(roleCode);
    public static bool CanViewTicketLookup(string? roleCode) => IsAnyApplicationRole(roleCode);
    public static bool CanUpdateApplication(string? roleCode) => IsAnyApplicationRole(roleCode);
    public static bool CanManageStations(string? roleCode) => IsAdmin(roleCode);

    public static bool IsSupportedRole(string? roleCode)
        => !string.IsNullOrWhiteSpace(roleCode) && StationRoles.SupportedRoles.Contains(roleCode);

    public static void EnsureAdmin(ICurrentUserContext currentUserContext, string capability)
    {
        if (!IsAdmin(currentUserContext.RoleCode))
        {
            throw new UnauthorizedAccessException($"Current user is not allowed to {capability}.");
        }
    }

    public static void EnsureSupportedRole(string roleCode)
    {
        if (!IsSupportedRole(roleCode))
        {
            throw new InvalidOperationException("Unsupported role code.");
        }
    }

    private static bool IsOperatorMasterDataStation(string? stationCode)
        => string.Equals(stationCode?.Trim(), "QN01", StringComparison.OrdinalIgnoreCase);
}
