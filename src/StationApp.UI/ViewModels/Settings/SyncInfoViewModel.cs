using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public partial class SyncInfoViewModel : ObservableObject
{
    private static readonly string[] MasterAggregateTypes =
    [
        SyncAggregateTypes.Vehicle,
        SyncAggregateTypes.Customer,
        SyncAggregateTypes.Product
    ];

    private readonly IServiceScopeFactory _scopeFactory;

    public SyncInfoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        AggregateTypeOptions = new ObservableCollection<string>(
        [
            "\u0054\u1ea5\u0074\u0020\u0063\u1ea3",
            "\u0110\u004b\u0050\u0054",
            "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e",
            "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e",
            "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0078\u0065",
            "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u006b\u0068\u00e1\u0063\u0068\u0020\u0068\u00e0\u006e\u0067",
            "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0073\u1ea3\u006e\u0020\u0070\u0068\u1ea9\u006d"
        ]);
        SelectedAggregateType = "\u0054\u1ea5\u0074\u0020\u0063\u1ea3";
    }

    [ObservableProperty] private int _pendingRegistrationsCount;
    [ObservableProperty] private int _pendingTicketsCount;
    [ObservableProperty] private int _pendingDeliveryTicketsCount;
    [ObservableProperty] private int _failedSyncCount;
    [ObservableProperty] private int _successSyncCount;
    [ObservableProperty] private int _unprocessedInboundCount;
    [ObservableProperty] private bool _isMetricsVisible;
    [ObservableProperty] private string _lastSyncError = "N/A";
    [ObservableProperty] private string _lastSyncSuccessAt = "N/A";
    [ObservableProperty] private string _lastSyncFailureAt = "N/A";
    [ObservableProperty] private ObservableCollection<SyncOutboxListItem> _syncItems = new();
    [ObservableProperty] private ObservableCollection<string> _aggregateTypeOptions = new(["\u0054\u1ea5\u0074\u0020\u0063\u1ea3", "\u0110\u004b\u0050\u0054", "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e", "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e"]);
    [ObservableProperty] private ObservableCollection<string> _outboxStatusOptions = new(["\u0054\u1ea5\u0074\u0020\u0063\u1ea3", "PENDING", "PROCESSING", "SUCCESS", "FAILED_RETRYABLE", "FAILED_FINAL"]);
    [ObservableProperty] private string _selectedAggregateType = "\u0054\u1ea5\u0074\u0020\u0063\u1ea3";
    [ObservableProperty] private string _selectedOutboxStatus = "\u0054\u1ea5\u0074\u0020\u0063\u1ea3";
    [ObservableProperty] private string? _searchKeyword;
    [ObservableProperty] private string _masterDataStatus = "Unknown";
    [ObservableProperty] private string _masterDataLastSync = "N/A";
    [ObservableProperty] private string _masterDataError = "N/A";
    [ObservableProperty] private int _vehicleMasterCount;
    [ObservableProperty] private int _customerMasterCount;
    [ObservableProperty] private int _productMasterCount;

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        try
        {
            PendingRegistrationsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.VehicleRegistration);
            PendingTicketsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.WeighTicket);
            PendingDeliveryTicketsCount = await CountPendingOutboxAsync(context, SyncAggregateTypes.DeliveryTicket);
            FailedSyncCount = await context.SyncOutbox
                .Where(o => o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.FAILED_FINAL)
                .CountAsync(CancellationToken.None);
            SuccessSyncCount = await context.SyncOutbox
                .Where(o => o.Status == OutboxStatus.SUCCESS)
                .CountAsync(CancellationToken.None);

            UnprocessedInboundCount = await context.VehicleRegistrations
                .Where(r => !r.IsInboundProcessed)
                .CountAsync(CancellationToken.None);

            var lastFailItem = await context.SyncOutbox
                .Where(o => !string.IsNullOrWhiteSpace(o.LastError))
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            var lastSuccessItem = await context.SyncOutbox
                .Where(o => o.Status == OutboxStatus.SUCCESS)
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            LastSyncError = lastFailItem?.LastError ?? "\u004b\u0068\u00f4\u006e\u0067\u0020\u0063\u00f3\u0020\u006c\u1ed7\u0069\u0020\u0067\u0068\u0069\u0020\u006e\u0068\u1ead\u006e\u002e";
            LastSyncFailureAt = FormatTimestamp(lastFailItem?.UpdatedAt ?? lastFailItem?.CreatedAt);
            LastSyncSuccessAt = FormatTimestamp(lastSuccessItem?.UpdatedAt ?? lastSuccessItem?.CreatedAt);
            SyncItems = new ObservableCollection<SyncOutboxListItem>(await LoadSyncItemsAsync(context));

            VehicleMasterCount = await context.Vehicles.AsNoTracking().CountAsync(CancellationToken.None);
            CustomerMasterCount = await context.Customers.AsNoTracking().CountAsync(CancellationToken.None);
            ProductMasterCount = await context.Products.AsNoTracking().CountAsync(CancellationToken.None);

            var pendingMasterCount = await context.SyncOutbox.AsNoTracking()
                .Where(o => MasterAggregateTypes.Contains(o.AggregateType)
                    && (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.PROCESSING))
                .CountAsync(CancellationToken.None);

            var lastMasterSuccess = await context.SyncOutbox.AsNoTracking()
                .Where(o => MasterAggregateTypes.Contains(o.AggregateType) && o.Status == OutboxStatus.SUCCESS)
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            var lastMasterFailure = await context.SyncOutbox.AsNoTracking()
                .Where(o => MasterAggregateTypes.Contains(o.AggregateType) && !string.IsNullOrWhiteSpace(o.LastError))
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            MasterDataStatus = pendingMasterCount > 0 ? $"Pending outbound ({pendingMasterCount})" : "No pending";
            MasterDataLastSync = FormatTimestamp(lastMasterSuccess?.UpdatedAt ?? lastMasterSuccess?.CreatedAt);
            MasterDataError = lastMasterFailure?.LastError ?? "N/A";
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("\u004c\u1ed7\u0069\u0020\u0068\u1ec7\u0020\u0074\u0068\u1ed1\u006e\u0067", $"\u004c\u1ed7\u0069\u0020\u0074\u0072\u0075\u0079\u0020\u0078\u0075\u1ea5\u0074\u0020\u0074\u0072\u1ea1\u006e\u0067\u0020\u0074\u0068\u00e1\u0069\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u003a {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        PendingRegistrationsCount = 0;
        PendingTicketsCount = 0;
        PendingDeliveryTicketsCount = 0;
        FailedSyncCount = 0;
        SuccessSyncCount = 0;
        UnprocessedInboundCount = 0;
        LastSyncError = "N/A";
        LastSyncSuccessAt = "N/A";
        LastSyncFailureAt = "N/A";
        SyncItems = new ObservableCollection<SyncOutboxListItem>();
        MasterDataStatus = "Unknown";
        MasterDataLastSync = "N/A";
        MasterDataError = "N/A";
        VehicleMasterCount = 0;
        CustomerMasterCount = 0;
        ProductMasterCount = 0;

        await LoadAsync();

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        await dialogService.ShowInfoAsync("\u0054\u0068\u00f4\u006e\u0067\u0020\u0062\u00e1\u006f", "\u0110\u00e3\u0020\u0063\u1ead\u0070\u0020\u006e\u0068\u1ead\u0074\u0020\u0064\u1eef\u0020\u006c\u0069\u1ec7\u0075\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u002e");
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        await dialogService.ShowInfoAsync("\u0054\u0068\u00f4\u006e\u0067\u0020\u0062\u00e1\u006f", "\u0059\u00ea\u0075\u0020\u0063\u1ea7\u0075\u0020\u0111\u1ea9\u0079\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u0020\u006e\u0067\u0061\u0079\u0020\u006c\u1ead\u0070\u0020\u0074\u1ee9\u0063\u0020\u0111\u00e3\u0020\u0111\u01b0\u1ee3\u0063\u0020\u0067\u1eed\u0069\u0020\u0074\u1edb\u0069\u0020\u0077\u006f\u0072\u006b\u0065\u0072\u002e");
    }

    [RelayCommand]
    private void ShowMetrics()
    {
        IsMetricsVisible = true;
    }

    [RelayCommand]
    private void CloseMetrics()
    {
        IsMetricsVisible = false;
    }

    private static Task<int> CountPendingOutboxAsync(StationDbContext context, string aggregateType)
    {
        return context.SyncOutbox
            .Where(o => o.AggregateType == aggregateType &&
                        (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE))
            .CountAsync(CancellationToken.None);
    }

    partial void OnSelectedAggregateTypeChanged(string value) => _ = LoadAsync();
    partial void OnSelectedOutboxStatusChanged(string value) => _ = LoadAsync();
    partial void OnSearchKeywordChanged(string? value) => _ = LoadAsync();

    private static string FormatTimestamp(DateTime? timestamp)
    {
        return timestamp?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
    }

    private async Task<IReadOnlyList<SyncOutboxListItem>> LoadSyncItemsAsync(StationDbContext context)
    {
        var query = context.SyncOutbox.AsNoTracking();

        if (SelectedAggregateType != "\u0054\u1ea5\u0074\u0020\u0063\u1ea3")
        {
            var aggregateType = SelectedAggregateType switch
            {
                "\u0110\u004b\u0050\u0054" => SyncAggregateTypes.VehicleRegistration,
                "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e" => SyncAggregateTypes.WeighTicket,
                "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e" => SyncAggregateTypes.DeliveryTicket,
                "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0078\u0065" => SyncAggregateTypes.Vehicle,
                "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u006b\u0068\u00e1\u0063\u0068\u0020\u0068\u00e0\u006e\u0067" => SyncAggregateTypes.Customer,
                "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0073\u1ea3\u006e\u0020\u0070\u0068\u1ea9\u006d" => SyncAggregateTypes.Product,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(aggregateType))
            {
                query = query.Where(x => x.AggregateType == aggregateType);
            }
        }

        if (SelectedOutboxStatus != "\u0054\u1ea5\u0074\u0020\u0063\u1ea3" && Enum.TryParse<OutboxStatus>(SelectedOutboxStatus, out var outboxStatus))
        {
            query = query.Where(x => x.Status == outboxStatus);
        }

        var outboxItems = await query
            .OrderBy(x => x.Status == OutboxStatus.PENDING ? 0 : x.Status == OutboxStatus.FAILED_RETRYABLE ? 1 : x.Status == OutboxStatus.PROCESSING ? 2 : 3)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(300)
            .ToListAsync(CancellationToken.None);

        var registrationIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.VehicleRegistration).Select(x => x.AggregateId).Distinct().ToList();
        var weighTicketIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.WeighTicket).Select(x => x.AggregateId).Distinct().ToList();
        var deliveryTicketIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.DeliveryTicket).Select(x => x.AggregateId).Distinct().ToList();
        var vehicleIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Vehicle).Select(x => x.AggregateId).Distinct().ToList();
        var customerIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Customer).Select(x => x.AggregateId).Distinct().ToList();
        var productIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Product).Select(x => x.AggregateId).Distinct().ToList();

        var registrations = await context.VehicleRegistrations.AsNoTracking()
            .Where(x => registrationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var weighTickets = await context.WeighTickets.AsNoTracking()
            .Where(x => weighTicketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var deliveryTickets = await context.DeliveryTickets.AsNoTracking()
            .Where(x => deliveryTicketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var vehicles = await context.Vehicles.AsNoTracking()
            .Where(x => vehicleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var customers = await context.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var products = await context.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);

        var sessionIds = registrations.Values.Where(x => x.WeighingSessionId.HasValue).Select(x => x.WeighingSessionId!.Value)
            .Concat(weighTickets.Values.Where(x => x.WeighingSessionId.HasValue).Select(x => x.WeighingSessionId!.Value))
            .Concat(deliveryTickets.Values.Where(x => x.WeighingSessionId.HasValue).Select(x => x.WeighingSessionId!.Value))
            .Distinct()
            .ToList();

        var sessions = await context.WeighingSessions.AsNoTracking()
            .Where(x => sessionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);

        var items = outboxItems.Select(item =>
        {
            var aggregateDisplay = GetAggregateTypeDisplay(item.AggregateType);
            var businessNo = string.Empty;
            var sessionNo = string.Empty;
            var vehiclePlate = string.Empty;
            var entitySyncStatus = "-";

            if (item.AggregateType == SyncAggregateTypes.VehicleRegistration && registrations.TryGetValue(item.AggregateId, out var registration))
            {
                businessNo = registration.ErpVehicleRegistrationId ?? registration.OrderCode ?? registration.Id.ToString("N")[..8];
                vehiclePlate = registration.VehiclePlate;
                entitySyncStatus = registration.SyncStatus.ToString();
                if (registration.WeighingSessionId.HasValue && sessions.TryGetValue(registration.WeighingSessionId.Value, out var registrationSession))
                {
                    sessionNo = registrationSession.SessionNo;
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.WeighTicket && weighTickets.TryGetValue(item.AggregateId, out var weighTicket))
            {
                businessNo = weighTicket.TicketNo;
                vehiclePlate = weighTicket.VehiclePlate;
                entitySyncStatus = weighTicket.SyncStatus.ToString();
                if (weighTicket.WeighingSessionId.HasValue && sessions.TryGetValue(weighTicket.WeighingSessionId.Value, out var ticketSession))
                {
                    sessionNo = ticketSession.SessionNo;
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.DeliveryTicket && deliveryTickets.TryGetValue(item.AggregateId, out var deliveryTicket))
            {
                businessNo = deliveryTicket.DeliveryNo;
                entitySyncStatus = deliveryTicket.SyncStatus.ToString();
                if (deliveryTicket.WeighingSessionId.HasValue && sessions.TryGetValue(deliveryTicket.WeighingSessionId.Value, out var deliverySession))
                {
                    sessionNo = deliverySession.SessionNo;
                    vehiclePlate = deliverySession.VehiclePlate;
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.Vehicle && vehicles.TryGetValue(item.AggregateId, out var vehicle))
            {
                businessNo = string.IsNullOrWhiteSpace(vehicle.MoocNumber)
                    ? vehicle.VehiclePlate
                    : $"{vehicle.VehiclePlate} ({vehicle.MoocNumber})";
                vehiclePlate = vehicle.VehiclePlate;
            }
            else if (item.AggregateType == SyncAggregateTypes.Customer && customers.TryGetValue(item.AggregateId, out var customer))
            {
                businessNo = customer.CustomerCode;
            }
            else if (item.AggregateType == SyncAggregateTypes.Product && products.TryGetValue(item.AggregateId, out var product))
            {
                businessNo = product.ProductCode;
            }

            return new SyncOutboxListItem(
                item.Id,
                aggregateDisplay,
                businessNo,
                sessionNo,
                vehiclePlate,
                entitySyncStatus,
                item.Status.ToString(),
                item.RetryCount,
                item.LastError,
                item.CreatedAt,
                item.UpdatedAt,
                item.NextRetryAt);
        });

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var keyword = SearchKeyword.Trim();
            items = items.Where(x =>
                x.AggregateType.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.BusinessNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.SessionNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.VehiclePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (x.LastError?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return items.ToList();
    }

    private static string GetAggregateTypeDisplay(string aggregateType)
    {
        return aggregateType switch
        {
            SyncAggregateTypes.VehicleRegistration => "\u0110\u004b\u0050\u0054",
            SyncAggregateTypes.WeighTicket => "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e",
            SyncAggregateTypes.DeliveryTicket => "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e",
            SyncAggregateTypes.Vehicle => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0078\u0065",
            SyncAggregateTypes.Customer => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u006b\u0068\u00e1\u0063\u0068\u0020\u0068\u00e0\u006e\u0067",
            SyncAggregateTypes.Product => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0073\u1ea3\u006e\u0020\u0070\u0068\u1ea9\u006d",
            _ => aggregateType
        };
    }
}

public sealed record SyncOutboxListItem(
    Guid OutboxId,
    string AggregateType,
    string BusinessNo,
    string SessionNo,
    string VehiclePlate,
    string EntitySyncStatus,
    string OutboxStatus,
    int RetryCount,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? NextRetryAt);
