using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.Interfaces;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public sealed partial class ExportTripTransferHistoryViewModel : ObservableObject
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWeighingSessionRepository _sessionRepository;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty] private string _title = "Lịch sử chuyển chuyến xe";
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<ExportTripTransferHistoryItemRow> _historyItems = new();

    public ExportTripTransferHistoryViewModel(
        IAuditLogRepository auditLogRepository,
        IWeighingSessionRepository sessionRepository,
        IToastService toastService,
        ICurrentUserContext currentUserContext)
    {
        _auditLogRepository = auditLogRepository;
        _sessionRepository = sessionRepository;
        _toastService = toastService;
        _currentUserContext = currentUserContext;
    }

    public async Task InitializeAsync()
    {
        await SearchAsync();
    }

    public void SetFilter(string? vehiclePlate, string? sessionNo)
    {
        SearchVehiclePlate = vehiclePlate;
        SearchSessionNo = sessionNo;
        // Extend FromDate to include this session if it is older than 7 days
        FromDate = DateTime.Today.AddDays(-30);
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        IsLoading = true;
        HistoryItems.Clear();

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Searching with StationCode: {_currentUserContext.StationCode}");

            var logs = await _auditLogRepository.SearchEditLogsAsync(
                SearchVehiclePlate,
                SearchSessionNo,
                FromDate,
                ToDate,
                _currentUserContext.StationCode,
                CancellationToken.None);

            System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Found {logs.Count} logs");

            var items = new List<ExportTripTransferHistoryItemRow>();
            int index = 1;

            foreach (var log in logs)
            {
                var row = new ExportTripTransferHistoryItemRow
                {
                    Index = index++,
                    CreatedAt = log.CreatedAt,
                    Actor = log.Actor
                };

                // Parse DetailJson for TRANSFER_EXPORT_TRIP
                if (!string.IsNullOrWhiteSpace(log.DetailJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(log.DetailJson);
                        var root = doc.RootElement;

                        System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Parsing log for session {log.EntityId}");

                        // Get SessionNo
                        if (root.TryGetProperty("SessionNo", out var sessionNoProp))
                        {
                            row.SessionNo = sessionNoProp.GetString() ?? string.Empty;
                            System.Diagnostics.Debug.WriteLine($"SessionNo: {row.SessionNo}");
                        }

                        // Get VehiclePlate
                        if (root.TryGetProperty("VehiclePlate", out var vehiclePlateProp))
                        {
                            row.VehiclePlate = vehiclePlateProp.GetString() ?? string.Empty;
                            System.Diagnostics.Debug.WriteLine($"VehiclePlate: {row.VehiclePlate}");
                        }

                        // Get GrossWeight
                        if (root.TryGetProperty("GrossWeight", out var grossWeightProp) && grossWeightProp.ValueKind != JsonValueKind.Null)
                        {
                            row.GrossWeight = grossWeightProp.GetDecimal();
                            System.Diagnostics.Debug.WriteLine($"GrossWeight: {row.GrossWeight}");
                        }

                        // Get ERP codes (preferred for display)
                        if (root.TryGetProperty("SourceErpCutOrderId", out var sourceErpCutOrderIdProp))
                        {
                            var erpCode = sourceErpCutOrderIdProp.GetString();
                            row.SourceErpCutOrderId = erpCode ?? string.Empty;
                        }

                        // Get display codes (fallback for temporary cut orders)
                        if (root.TryGetProperty("SourceDisplayCode", out var sourceDisplayCodeProp))
                        {
                            row.SourceDisplayCode = sourceDisplayCodeProp.GetString() ?? string.Empty;
                        }

                        if (root.TryGetProperty("TargetErpCutOrderId", out var targetErpCutOrderIdProp))
                        {
                            var erpCode = targetErpCutOrderIdProp.GetString();
                            row.TargetErpCutOrderId = erpCode ?? string.Empty;
                        }

                        // Get display codes (fallback for temporary cut orders)
                        if (root.TryGetProperty("TargetDisplayCode", out var targetDisplayCodeProp))
                        {
                            row.TargetDisplayCode = targetDisplayCodeProp.GetString() ?? string.Empty;
                        }

                        // Also parse GUID fields (for internal use, not display)
                        if (root.TryGetProperty("SourceCutOrderId", out var sourceCutOrderIdProp))
                        {
                            row.SourceCutOrderId = sourceCutOrderIdProp.GetString() ?? string.Empty;
                        }

                        if (root.TryGetProperty("TargetCutOrderId", out var targetCutOrderIdProp))
                        {
                            row.TargetCutOrderId = targetCutOrderIdProp.GetString() ?? string.Empty;
                        }

                        // Get Reason
                        if (root.TryGetProperty("Reason", out var reasonProp))
                        {
                            row.TransferReason = reasonProp.GetString();
                            System.Diagnostics.Debug.WriteLine($"TransferReason: {row.TransferReason}");
                        }

                        System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Parsed: Source={row.SourceErpCutOrderId} -> Target={row.TargetErpCutOrderId}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Parse error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] JSON: {log.DetailJson}");
                        row.TransferReason = "Lỗi đọc dữ liệu chuyển chuyến.";
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Empty DetailJson for log {log.Id}");
                }

                // Fallback: Resolve SessionNo and VehiclePlate from entity if not set
                if (string.IsNullOrWhiteSpace(row.SessionNo))
                {
                    try
                    {
                        var session = await _sessionRepository.GetByIdAsync(log.EntityId, CancellationToken.None);
                        if (session != null)
                        {
                            row.SessionNo = session.SessionNo;
                            if (string.IsNullOrWhiteSpace(row.VehiclePlate))
                            {
                                row.VehiclePlate = session.VehiclePlate ?? string.Empty;
                            }
                            if ((row.GrossWeight == null || row.GrossWeight == 0) && session.Weight1.HasValue)
                            {
                                row.GrossWeight = session.Weight1;
                            }
                        }
                    }
                    catch
                    {
                        if (string.IsNullOrWhiteSpace(row.SessionNo))
                        {
                            row.SessionNo = log.EntityId.ToString().Substring(0, 8);
                        }
                    }
                }

                items.Add(row);
            }

            HistoryItems = new ObservableCollection<ExportTripTransferHistoryItemRow>(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ExportTripTransferHistory] StackTrace: {ex.StackTrace}");
            _toastService.ShowError($"Không thể tải lịch sử chuyển chuyến: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchVehiclePlate = null;
        SearchSessionNo = null;
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Today;
        await SearchAsync();
    }
}