using System.Globalization;
using System.Net;
using StationApp.Domain.Entities;

namespace StationApp.Application.Services;

public static class TelegramNotificationMessageBuilder
{
    public static string BuildExportTripTransfer(
        WeighingSession session,
        CutOrder sourceCutOrder,
        CutOrder targetCutOrder,
        string? sourceDisplayCode,
        string? targetDisplayCode,
        string username,
        string? displayName,
        string? stationCode,
        DateTime occurredAt)
    {
        return string.Join("\n", new[]
        {
            "<b>CẢNH BÁO: CHUYỂN CHUYẾN XE XUẤT KHẨU</b>",
            Line("Thời gian", FormatDateTime(occurredAt)),
            Line("Người thao tác", FormatActor(username, displayName)),
            Line("Trạm", stationCode),
            Line("Số phiếu", session.SessionNo),
            Line("Biển số xe", session.VehiclePlate),
            Line("Từ cắt lệnh", sourceDisplayCode ?? sourceCutOrder.ErpCutOrderId ?? sourceCutOrder.Id.ToString()),
            Line("Sang cắt lệnh", targetDisplayCode ?? targetCutOrder.ErpCutOrderId ?? targetCutOrder.Id.ToString()),
            Line("Khách hàng", targetCutOrder.CustomerName),
            Line("Hàng hóa", targetCutOrder.ProductName),
            Line("Cân lần 1", FormatWeight(session.Weight1)),
            Line("Cân lần 2", FormatWeight(session.Weight2)),
            Line("TL hàng", FormatWeight(session.NetWeight))
        });
    }

    public static string BuildClayTripTransfer(
        WeighingSession session,
        CutOrder sourceVessel,
        CutOrder targetVessel,
        string username,
        string? displayName,
        string? stationCode,
        DateTime occurredAt)
    {
        return string.Join("\n", new[]
        {
            "<b>CẢNH BÁO: CHUYỂN CHUYẾN XE MỎ SÉT</b>",
            Line("Thời gian", FormatDateTime(occurredAt)),
            Line("Người thao tác", FormatActor(username, displayName)),
            Line("Trạm", stationCode),
            Line("Số phiếu", session.SessionNo),
            Line("Biển số xe", session.VehiclePlate),
            Line("Từ tàu", sourceVessel.VehiclePlate),
            Line("Sang tàu", targetVessel.VehiclePlate),
            Line("Khách hàng", targetVessel.CustomerName),
            Line("Hàng hóa", targetVessel.ProductName),
            Line("Cân lần 1", FormatWeight(session.Weight1)),
            Line("Cân lần 2", FormatWeight(session.Weight2)),
            Line("TL hàng", FormatWeight(session.NetWeight))
        });
    }

    public static string BuildVehicleEdit(
        string title,
        WeighingSession session,
        string oldVehiclePlate,
        string newVehiclePlate,
        string reason,
        string username,
        string? displayName,
        string? stationCode,
        DateTime occurredAt)
    {
        var displayTitle = NormalizeVehicleEditTitle(title);
        return string.Join("\n", new[]
        {
            $"<b>{Html(displayTitle)}</b>",
            Line("Thời gian", FormatDateTime(occurredAt)),
            Line("Người thao tác", FormatActor(username, displayName)),
            Line("Trạm", stationCode),
            Line("Số phiếu", session.SessionNo),
            Line("Xe cũ", oldVehiclePlate),
            Line("Xe mới", newVehiclePlate),
            Line("Lý do", reason),
            Line("Cân lần 1", FormatWeight(session.Weight1)),
            Line("Cân lần 2", FormatWeight(session.Weight2)),
            Line("TL hàng", FormatWeight(session.NetWeight))
        });
    }

    private static string NormalizeVehicleEditTitle(string title)
        => title.Contains("XE", StringComparison.OrdinalIgnoreCase)
            ? "CẢNH BÁO: ĐỔI SỐ XE MỎ ĐÁ"
            : title;

    private static string Line(string label, object? value)
        => $"<b>{Html(label)}:</b> {Html(value?.ToString())}";

    private static string FormatActor(string username, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.Equals(username, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return username;
        }

        return $"{displayName} ({username})";
    }

    private static string FormatWeight(decimal? value)
        => value.HasValue ? $"{value.Value.ToString("N0", CultureInfo.InvariantCulture)} kg" : "--";

    private static string FormatDateTime(DateTime value)
        => value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Html(string? value)
        => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "--" : value.Trim());
}
