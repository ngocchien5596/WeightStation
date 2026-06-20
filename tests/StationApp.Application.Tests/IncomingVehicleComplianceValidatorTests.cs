using StationApp.Application.DTOs;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class IncomingVehicleComplianceValidatorTests
{
    private static readonly IncomingVehicleComplianceRules EnabledRules = new(
        new IncomingVehicleComplianceRuleSet(true, true),
        new IncomingVehicleComplianceRuleSet(true, true));

    [Fact]
    public void ValidateForCreateSession_ReturnsNull_ForNonOutbound()
    {
        var result = IncomingVehicleComplianceValidator.ValidateForCreateSession(
            EnabledRules,
            new IncomingVehicleComplianceValidationInput(
                "QN01",
                TransactionType.INBOUND,
                ProductTypes.Bagged,
                null,
                null,
                null,
                null,
                null,
                null));

        Assert.Null(result);
    }

    [Fact]
    public void ValidateForCreateSession_RequiresTtcp_ForBaggedOutbound()
    {
        var result = IncomingVehicleComplianceValidator.ValidateForCreateSession(
            EnabledRules,
            new IncomingVehicleComplianceValidationInput(
                "QN01",
                TransactionType.OUTBOUND,
                ProductTypes.Bagged,
                null,
                "DKX-01",
                new DateTime(2027, 1, 1),
                null,
                null,
                null));

        Assert.Equal("Bắt buộc nhập TTCP trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.", result);
    }

    [Fact]
    public void ValidateForCreateSession_RequiresVehicleRegistration_WhenRuleEnabled()
    {
        var result = IncomingVehicleComplianceValidator.ValidateForCreateSession(
            EnabledRules,
            new IncomingVehicleComplianceValidationInput(
                "QN01",
                TransactionType.OUTBOUND,
                ProductTypes.Bulk,
                5500m,
                null,
                null,
                null,
                null,
                null));

        Assert.Equal("Bắt buộc nhập Số ĐK xe và Hạn ĐK xe trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.", result);
    }

    [Fact]
    public void ValidateForCreateSession_RequiresMoocRegistration_WhenMoocExists()
    {
        var result = IncomingVehicleComplianceValidator.ValidateForCreateSession(
            EnabledRules,
            new IncomingVehicleComplianceValidationInput(
                "QN01",
                TransactionType.OUTBOUND,
                ProductTypes.Bagged,
                5500m,
                "DKX-01",
                new DateTime(2027, 1, 1),
                "MOOC-01",
                null,
                null));

        Assert.Equal("Xe có mooc nên bắt buộc nhập đủ Số ĐK mooc và Hạn ĐK mooc trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.", result);
    }

    [Fact]
    public void ValidateForCreateSession_ReturnsNull_WhenAllRequiredFieldsPresent()
    {
        var result = IncomingVehicleComplianceValidator.ValidateForCreateSession(
            EnabledRules,
            new IncomingVehicleComplianceValidationInput(
                "QN01",
                TransactionType.OUTBOUND,
                ProductTypes.Bagged,
                5500m,
                "DKX-01",
                new DateTime(2027, 1, 1),
                "MOOC-01",
                "DKM-01",
                new DateTime(2027, 2, 1)));

        Assert.Null(result);
    }
}
