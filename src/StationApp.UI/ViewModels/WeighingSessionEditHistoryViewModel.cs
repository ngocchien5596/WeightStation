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

public sealed class EditHistoryItemRow
{
    public int Index { get; set; }
    public string SessionNo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string OldVehiclePlate { get; set; } = string.Empty;
    public string NewVehiclePlate { get; set; } = string.Empty;
    public decimal? GrossWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public decimal? OldStandardTare { get; set; }
    public decimal? NewStandardTare { get; set; }
    public decimal? OldNetWeight { get; set; }
    public decimal? NewNetWeight { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed partial class WeighingSessionEditHistoryViewModel : ObservableObject
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWeighingSessionRepository _sessionRepository;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty] private string _title = "Lịch sử sửa số liệu cân";
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<EditHistoryItemRow> _historyItems = new();

    public WeighingSessionEditHistoryViewModel(
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
            System.Diagnostics.Debug.WriteLine($"[EditHistory] Searching with StationCode: {_currentUserContext.StationCode}");

            var logs = await _auditLogRepository.SearchEditLogsAsync(
                SearchVehiclePlate,
                SearchSessionNo,
                FromDate,
                ToDate,
                _currentUserContext.StationCode,
                CancellationToken.None);

            System.Diagnostics.Debug.WriteLine($"[EditHistory] Found {logs.Count} logs");

            var items = new List<EditHistoryItemRow>();
            int index = 1;

            foreach (var log in logs)
            {
                var row = new EditHistoryItemRow
                {
                    Index = index++,
                    CreatedAt = log.CreatedAt,
                    Actor = log.Actor
                };

                System.Diagnostics.Debug.WriteLine($"[EditHistory] Processing log {log.Id}, Action={log.Action}");

                // Parse DetailJson
                if (!string.IsNullOrWhiteSpace(log.DetailJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(log.DetailJson);
                        var root = doc.RootElement;

                        System.Diagnostics.Debug.WriteLine($"[EditHistory] Parsed JSON for log {log.Id}, Action={log.Action}, DetailJson length={log.DetailJson.Length}");

                        // Handle different action types
                        if (log.Action == "EDIT_WEIGHING_SESSION")
                        {
                            // Parse EDIT_WEIGHING_SESSION structure
                            if (root.TryGetProperty("Reason", out var reasonProp))
                            {
                                row.Reason = reasonProp.GetString() ?? string.Empty;
                            }

                            if (root.TryGetProperty("Changes", out var changesProp))
                            {
                                if (changesProp.TryGetProperty("VehiclePlate", out var plateProp))
                                {
                                    row.OldVehiclePlate = plateProp.GetProperty("Old").GetString() ?? string.Empty;
                                    row.NewVehiclePlate = plateProp.GetProperty("New").GetString() ?? string.Empty;
                                }

                                if (changesProp.TryGetProperty("StandardTareWeightSnapshot", out var tareProp))
                                {
                                    if (tareProp.GetProperty("Old").ValueKind != JsonValueKind.Null)
                                        row.OldStandardTare = tareProp.GetProperty("Old").GetDecimal();
                                    if (tareProp.GetProperty("New").ValueKind != JsonValueKind.Null)
                                        row.NewStandardTare = tareProp.GetProperty("New").GetDecimal();
                                }

                                if (changesProp.TryGetProperty("NetWeight", out var netProp))
                                {
                                    if (netProp.GetProperty("Old").ValueKind != JsonValueKind.Null)
                                        row.OldNetWeight = netProp.GetProperty("Old").GetDecimal();
                                    if (netProp.GetProperty("New").ValueKind != JsonValueKind.Null)
                                    {
                                        row.NewNetWeight = netProp.GetProperty("New").GetDecimal();
                                        row.NetWeight = row.NewNetWeight;
                                    }
                                    else
                                    {
                                        row.NetWeight = row.OldNetWeight;
                                    }
                                }
                            }
                        }
                        else if (log.Action == "TRANSFER_EXPORT_TRIP")
                        {
                            // Parse TRANSFER_EXPORT_TRIP structure
                            if (root.TryGetProperty("SessionNo", out var sessionNoProp))
                            {
                                row.SessionNo = sessionNoProp.GetString() ?? string.Empty;
                            }

                            if (root.TryGetProperty("VehiclePlate", out var vehiclePlateProp))
                            {
                                row.OldVehiclePlate = vehiclePlateProp.GetString() ?? string.Empty;
                                // For transfer trips, show the same plate in both columns or indicate it's a transfer
                                row.NewVehiclePlate = "(Chuyển chuyến)";
                            }

                            if (root.TryGetProperty("Reason", out var reasonProp))
                            {
                                row.Reason = reasonProp.GetString() ?? string.Empty;
                            }

                            if (root.TryGetProperty("NetWeight", out var netWeightProp))
                            {
                                if (netWeightProp.ValueKind == JsonValueKind.Null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[EditHistory] TRANSFER_EXPORT_TRIP: NetWeight property is NULL for log {log.Id}");
                                }
                                else
                                {
                                    var netWeight = netWeightProp.GetDecimal();
                                    row.NetWeight = netWeight;
                                    row.OldNetWeight = netWeight;
                                    row.NewNetWeight = netWeight;
                                    System.Diagnostics.Debug.WriteLine($"[EditHistory] TRANSFER_EXPORT_TRIP: Set NetWeight={netWeight}, OldNetWeight={netWeight}, NewNetWeight={netWeight} for SessionNo={row.SessionNo}, LogId={log.Id}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[EditHistory] TRANSFER_EXPORT_TRIP: NetWeight property NOT FOUND in JSON for log {log.Id}");
                                System.Diagnostics.Debug.WriteLine($"[EditHistory] Available properties: {string.Join(", ", root.EnumerateObject().Select(p => p.Name))}");
                            }

                            if (root.TryGetProperty("GrossWeight", out var grossWeightProp) && grossWeightProp.ValueKind != JsonValueKind.Null)
                            {
                                row.GrossWeight = grossWeightProp.GetDecimal();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        row.Reason = $"Lỗi đọc dữ liệu chi tiết sửa đổi: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"[EditHistory] Error parsing log {log.Id}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[EditHistory] DetailJson: {log.DetailJson}");
                        System.Diagnostics.Debug.WriteLine($"[EditHistory] StackTrace: {ex.StackTrace}");
                    }
                }

                // Fallback: Resolve SessionNo from entity if not already set
                if (string.IsNullOrWhiteSpace(row.SessionNo))
                {
                    try
                    {
                        var session = await _sessionRepository.GetByIdAsync(log.EntityId, CancellationToken.None);
                        if (session != null)
                        {
                            row.SessionNo = session.SessionNo;
                            if (row.GrossWeight == null || row.GrossWeight == 0)
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

            HistoryItems = new ObservableCollection<EditHistoryItemRow>(items);

            // Log final results
            System.Diagnostics.Debug.WriteLine($"[EditHistory] === FINAL RESULTS ===");
            foreach (var item in items)
            {
                System.Diagnostics.Debug.WriteLine($"[EditHistory] Item {item.Index}: SessionNo={item.SessionNo}, NetWeight={item.NetWeight}, OldNetWeight={item.OldNetWeight}, NewNetWeight={item.NewNetWeight}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditHistory] Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[EditHistory] StackTrace: {ex.StackTrace}");
            _toastService.ShowError($"Không thể tải lịch sử sửa số liệu: {ex.Message}");
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
