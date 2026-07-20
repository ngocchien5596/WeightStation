using System.Text.Json;
using StationApp.Application.Services;
using StationApp.Domain.Entities;
using Xunit;

namespace StationApp.Application.Tests;

public sealed class AuditLogDisplayMapperTests
{
    [Fact]
    public void Map_ChangesPayload_RendersOldAndNewValuesAsKeyValueLines()
    {
        var log = NewLog(
            "EDIT_WEIGHING_SESSION",
            """
            {
              "Reason": "Đổi xe",
              "Changes": {
                "VehiclePlate": { "Old": "08", "New": "04" },
                "StandardTareWeightSnapshot": { "Old": 20000, "New": null },
                "NetWeight": { "Old": 38000, "New": 34000 }
              }
            }
            """);

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("Đổi số xe", row.ActionDisplay);
        Assert.Contains("Số xe: 08", row.OldValueDisplay);
        Assert.Contains("Số xe: 04", row.NewValueDisplay);
        Assert.Contains("TL bì: 20,000 kg", row.OldValueDisplay);
        Assert.Contains("TL bì: --", row.NewValueDisplay);
        Assert.Contains("TL hàng: 38,000 kg", row.OldValueDisplay);
        Assert.Contains("TL hàng: 34,000 kg", row.NewValueDisplay);
        Assert.Equal("Đổi xe", row.Note);
    }

    [Fact]
    public void Map_OldNewPayload_DiffsChangedFieldsOnly()
    {
        var log = NewLog(
            "UPDATE_CLAY_VESSEL",
            """
            {
              "VesselName": "Tàu 04",
              "Old": {
                "CustomerName": "vina",
                "ProductName": "Sét",
                "Notes": "giữ nguyên"
              },
              "New": {
                "CustomerName": "Minh Long",
                "ProductName": "Sỉ",
                "Notes": "giữ nguyên"
              },
              "UpdatedSessionCount": 2,
              "UpdatedLineCount": 3
            }
            """);

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("Sửa tàu mỏ sét", row.ActionDisplay);
        Assert.Equal("Tàu 04", row.EntityDisplay);
        Assert.Contains("Khách hàng: vina", row.OldValueDisplay);
        Assert.Contains("Khách hàng: Minh Long", row.NewValueDisplay);
        Assert.Contains("Hàng hóa: Sét", row.OldValueDisplay);
        Assert.Contains("Hàng hóa: Sỉ", row.NewValueDisplay);
        Assert.DoesNotContain("Ghi chú", row.OldValueDisplay);
        Assert.Contains("Đã cập nhật 2 chuyến xe, 3 dòng chuyến", row.DetailSummary);
    }

    [Fact]
    public void Map_ReturnedBrokenTripPayload_RendersRecognizedReturnedWeight()
    {
        var log = NewLog(
            "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP",
            """
            {
              "SessionNo": "LC26070055",
              "VehiclePlate": "37C-11796",
              "OldNetWeight": 49000,
              "ActualReturnedWeight": 49500,
              "ReturnedRecognizedWeight": 49000,
              "IsReturnedWeightCapped": true,
              "OldIsReturnedBrokenTrip": false,
              "NewIsReturnedBrokenTrip": true,
              "Note": "Đánh dấu hàng hoàn mỏ đá"
            }
            """);

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("Cập nhật hàng hoàn mỏ đá", row.ActionDisplay);
        Assert.Equal("LC26070055", row.EntityDisplay);
        Assert.Contains("Hàng hoàn: Không", row.OldValueDisplay);
        Assert.Contains("Hàng hoàn: Có", row.NewValueDisplay);
        Assert.Contains("TL hoàn thực cân: 49,500 kg", row.OldValueDisplay);
        Assert.Contains("TL hoàn ghi nhận: 49,000 kg", row.NewValueDisplay);
        Assert.Contains("giới hạn theo chuyến gần nhất", row.DetailSummary);
    }

    [Fact]
    public void Map_FallbackPayload_UsesMeaningfulTextInsteadOfOpaqueIds()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var log = NewLog(
            "TRANSFER_CLAY_TRIP",
            $$"""
            {
              "SessionNo": "LC26070020",
              "SourceCutOrderId": "{{sourceId}}",
              "SourceVesselName": "Tàu 01",
              "TargetCutOrderId": "{{targetId}}",
              "TargetVesselName": "Tàu 02",
              "VehiclePlate": "04"
            }
            """);

        var row = AuditLogDisplayMapper.Map(log);

        Assert.DoesNotContain(sourceId.ToString(), row.NewValueDisplay);
        Assert.DoesNotContain(targetId.ToString(), row.NewValueDisplay);
        Assert.Contains("Tàu nguồn: Tàu 01", row.NewValueDisplay);
        Assert.Contains("Tàu đích: Tàu 02", row.NewValueDisplay);
    }

    [Fact]
    public void Map_UserPayload_UsesUsernameAsEntityDisplay()
    {
        var entityId = Guid.NewGuid();
        var log = NewLog(
            "CREATE_USER_ACCOUNT",
            """
            {
              "Username": "chienbn2",
              "RoleCode": "MANAGER"
            }
            """,
            entityId);

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("chienbn2", row.EntityDisplay);
        Assert.NotEqual(entityId.ToString("N")[..8], row.EntityDisplay);
        Assert.Contains("Tên đăng nhập: chienbn2", row.NewValueDisplay);
    }

    [Fact]
    public void Map_NormalizedChangesPayload_RendersArrayAndTransferOldNewValues()
    {
        var detail = new AuditLogDetailBuilder()
            .WithSubject("Name", "LC26070020")
            .AddChange("CutOrder", "CL-TAM-01", "CL-XK-02")
            .AddChange("StationAssignments", new[] { "QN01" }, new[] { "QN01", "QN02 (mặc định)" })
            .Build();
        var log = NewLog("TRANSFER_EXPORT_TRIP", JsonSerializer.Serialize(detail));

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("LC26070020", row.EntityDisplay);
        Assert.Contains("Cắt lệnh: CL-TAM-01", row.OldValueDisplay);
        Assert.Contains("Cắt lệnh: CL-XK-02", row.NewValueDisplay);
        Assert.Contains("Phân quyền trạm: QN01", row.OldValueDisplay);
        Assert.Contains("Phân quyền trạm: QN01, QN02 (mặc định)", row.NewValueDisplay);
    }

    [Fact]
    public void Map_DomesticReturnedGoodsPayload_RendersReturnedGoodsChange()
    {
        var detail = new AuditLogDetailBuilder()
            .WithSubject(nameof(WeighingSession.SessionNo), "LC26070080")
            .WithSubject(nameof(WeighingSession.VehiclePlate), "14C-12345")
            .AddChange(nameof(WeighingSession.IsReturnedBrokenTrip), false, true)
            .WithSummary(nameof(WeighingSession.NetWeight), 25000m)
            .AddNote("Đánh dấu xe hàng hoàn nội địa, không tính vào báo cáo xuất hàng nội địa.")
            .Build();
        var log = NewLog("TOGGLE_DOMESTIC_RETURNED_GOODS", JsonSerializer.Serialize(detail));

        var row = AuditLogDisplayMapper.Map(log);

        Assert.Equal("Cập nhật hoàn hàng nội địa", row.ActionDisplay);
        Assert.Equal("LC26070080", row.EntityDisplay);
        Assert.Contains("Hàng hoàn: Không", row.OldValueDisplay);
        Assert.Contains("Hàng hoàn: Có", row.NewValueDisplay);
        Assert.Contains("không tính vào báo cáo xuất hàng nội địa", row.Note);
    }
    [Fact]
    public void KnownActions_AllHaveVietnameseDisplayNames()
    {
        foreach (var action in AuditLogDisplayMapper.KnownActions)
        {
            var display = AuditLogDisplayMapper.ToActionDisplay(action);

            Assert.False(string.Equals(action, display, StringComparison.Ordinal), action);
            Assert.DoesNotContain("_", display);
        }
    }

    private static AuditLog NewLog(string action, string detailJson, Guid? entityId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Action = action,
            Actor = "tester",
            EntityType = "WeighingSession",
            EntityId = entityId ?? Guid.NewGuid(),
            DetailJson = detailJson,
            CreatedAt = new DateTime(2026, 7, 16, 8, 30, 0),
            StationCode = "QN02"
        };
}
