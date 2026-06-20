namespace StationApp.Domain.Constants;

public static class IncomingVehicleOperationSettingKeys
{
    public const string RequireTtcpForBaggedOutbound = "incoming_require_ttcp_for_bagged_outbound";
    public const string RequireRegistrationForBaggedOutbound = "incoming_require_registration_for_bagged_outbound";
    public const string RequireTtcpForBulkOutbound = "incoming_require_ttcp_for_bulk_outbound";
    public const string RequireRegistrationForBulkOutbound = "incoming_require_registration_for_bulk_outbound";
}
