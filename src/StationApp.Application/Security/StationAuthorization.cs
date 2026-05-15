using StationApp.Application.Interfaces;

namespace StationApp.Application.Security;

public static class StationRoles
{
    public const string Admin = "ADMIN";
    public const string Operator = "OPERATOR";

    public static readonly IReadOnlySet<string> SupportedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        Operator
    };
}

public static class StationAuthorization
{
    public static bool IsAdmin(string? roleCode)
        => string.Equals(roleCode, StationRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsOperator(string? roleCode)
        => string.Equals(roleCode, StationRoles.Operator, StringComparison.OrdinalIgnoreCase);

    public static bool CanUseManualWeighing(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManageAccounts(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManageSystemSettings(string? roleCode) => IsAdmin(roleCode);
    public static bool CanManageDeviceConfiguration(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewDiagnostics(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewSettingsAdministration(string? roleCode) => IsAdmin(roleCode);
    public static bool CanViewMasterData(string? roleCode) => IsAdmin(roleCode) || IsOperator(roleCode);
    public static bool CanViewOperationalScreens(string? roleCode) => IsAdmin(roleCode) || IsOperator(roleCode);
    public static bool CanViewTicketLookup(string? roleCode) => IsAdmin(roleCode) || IsOperator(roleCode);

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
}
