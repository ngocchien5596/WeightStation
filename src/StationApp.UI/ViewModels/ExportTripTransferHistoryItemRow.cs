namespace StationApp.UI.ViewModels;

public sealed class ExportTripTransferHistoryItemRow
{
    public int Index { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string SessionNo { get; set; } = string.Empty;
    public string VehiclePlate { get; set; } = string.Empty;
    public decimal? GrossWeight { get; set; }
    public string SourceCutOrderId { get; set; } = string.Empty;
    public string SourceErpCutOrderId { get; set; } = string.Empty;
    public string SourceDisplayCode { get; set; } = string.Empty;
    public string TargetCutOrderId { get; set; } = string.Empty;
    public string TargetErpCutOrderId { get; set; } = string.Empty;
    public string TargetDisplayCode { get; set; } = string.Empty;
    public string? TransferReason { get; set; }

    /// <summary>Hiển thị mã cắt lệnh nguồn: ưu tiên ERP code, fallback sang DisplayCode.</summary>
    public string SourceCodeDisplay => !string.IsNullOrWhiteSpace(SourceErpCutOrderId)
        ? SourceErpCutOrderId
        : !string.IsNullOrWhiteSpace(SourceDisplayCode)
            ? SourceDisplayCode
            : SourceCutOrderId;

    /// <summary>Hiển thị mã cắt lệnh đích: ưu tiên ERP code, fallback sang DisplayCode.</summary>
    public string TargetCodeDisplay => !string.IsNullOrWhiteSpace(TargetErpCutOrderId)
        ? TargetErpCutOrderId
        : !string.IsNullOrWhiteSpace(TargetDisplayCode)
            ? TargetDisplayCode
            : TargetCutOrderId;
}