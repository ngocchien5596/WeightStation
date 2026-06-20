using StationApp.Domain.Constants;
using StationApp.Domain.Enums;

namespace StationApp.Application.DTOs;

public sealed record IncomingVehicleComplianceRuleSet(
    bool RequireTtcpOnCreateSession,
    bool RequireRegistrationOnCreateSession);

public sealed record IncomingVehicleComplianceRules(
    IncomingVehicleComplianceRuleSet BaggedOutbound,
    IncomingVehicleComplianceRuleSet BulkOutbound)
{
    public static IncomingVehicleComplianceRules Disabled { get; } = new(
        new IncomingVehicleComplianceRuleSet(false, false),
        new IncomingVehicleComplianceRuleSet(false, false));
}

public sealed record IncomingVehicleComplianceValidationInput(
    string? StationCode,
    TransactionType TransactionType,
    string? ProductType,
    decimal? TtcpWeight,
    string? VehicleRegistrationNo,
    DateTime? VehicleRegistrationExpiry,
    string? MoocNumber,
    string? MoocRegistrationNo,
    DateTime? MoocRegistrationExpiry);

public static class IncomingVehicleComplianceValidator
{
    public static string? ValidateForCreateSession(
        IncomingVehicleComplianceRules rules,
        IncomingVehicleComplianceValidationInput input)
    {
        if (input.TransactionType != TransactionType.OUTBOUND)
        {
            return null;
        }

        var normalizedProductType = ProductTypes.Normalize(input.ProductType);
        var rule = ResolveRule(rules, normalizedProductType);
        if (rule == null)
        {
            return null;
        }

        var stationCode = string.IsNullOrWhiteSpace(input.StationCode)
            ? "trạm hiện tại"
            : input.StationCode.Trim().ToUpperInvariant();

        if (rule.RequireTtcpOnCreateSession && (!input.TtcpWeight.HasValue || input.TtcpWeight.Value <= 0))
        {
            return $"Bắt buộc nhập TTCP trước khi chuyển xe vào Cân nội địa theo cấu hình trạm {stationCode}.";
        }

        if (!rule.RequireRegistrationOnCreateSession)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(input.VehicleRegistrationNo) || !input.VehicleRegistrationExpiry.HasValue)
        {
            return $"Bắt buộc nhập Số ĐK xe và Hạn ĐK xe trước khi chuyển xe vào Cân nội địa theo cấu hình trạm {stationCode}.";
        }

        if (!string.IsNullOrWhiteSpace(input.MoocNumber)
            && (string.IsNullOrWhiteSpace(input.MoocRegistrationNo) || !input.MoocRegistrationExpiry.HasValue))
        {
            return $"Xe có mooc nên bắt buộc nhập đủ Số ĐK mooc và Hạn ĐK mooc trước khi chuyển xe vào Cân nội địa theo cấu hình trạm {stationCode}.";
        }

        return null;
    }

    private static IncomingVehicleComplianceRuleSet? ResolveRule(
        IncomingVehicleComplianceRules rules,
        string? normalizedProductType)
    {
        if (string.Equals(normalizedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase))
        {
            return rules.BaggedOutbound;
        }

        if (string.Equals(normalizedProductType, ProductTypes.Bulk, StringComparison.OrdinalIgnoreCase))
        {
            return rules.BulkOutbound;
        }

        return null;
    }
}
