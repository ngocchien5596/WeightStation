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
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public partial class SyncInfoViewModel : ObservableObject
{
    private const int DefaultPageSize = 100;

    private static readonly string[] MasterAggregateTypes =
    [
        SyncAggregateTypes.Station,
        SyncAggregateTypes.Vehicle,
        SyncAggregateTypes.Customer,
        SyncAggregateTypes.Product
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private CancellationTokenSource? _searchDebounceCts;

    public SyncInfoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        AggregateTypeOptions = new ObservableCollection<string>(
        [
            "\u0054\u1ea5\u0074\u0020\u0063\u1ea3",
            "\u0110\u004b\u0050\u0054",
            "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e",
            "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e",
            "\u0050\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e",
            "\u0044\u00f2\u006e\u0067\u0020\u0070\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e",
            "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0074\u0072\u1ea1\u006d",
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
    [ObservableProperty] private ObservableCollection<string> _aggregateTypeOptions = new(["\u0054\u1ea5\u0074\u0020\u0063\u1ea3", "\u0110\u004b\u0050\u0054", "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e", "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e", "\u0050\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e", "\u0044\u00f2\u006e\u0067\u0020\u0070\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e", "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0074\u0072\u1ea1\u006d", "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0078\u0065", "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u006b\u0068\u00e1\u0063\u0068\u0020\u0068\u00e0\u006e\u0067", "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0073\u1ea3\u006e\u0020\u0070\u0068\u1ea9\u006d"]);
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
    [ObservableProperty] private SyncOutboxListItem? _selectedSyncItem;
    [ObservableProperty] private int _pageNumber = 1;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _totalSyncItemsCount;
    [ObservableProperty] private string _pageSummary = "Không có dữ liệu";
    [ObservableProperty] private bool _canGoToPreviousPage;
    [ObservableProperty] private bool _canGoToNextPage;

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var stationScope = scope.ServiceProvider.GetRequiredService<IStationScope>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var stationCode = await stationScope.GetCurrentStationCodeAsync(CancellationToken.None);

        try
        {
            PendingRegistrationsCount = await CountPendingOutboxAsync(context, stationCode, SyncAggregateTypes.CutOrder);
            PendingTicketsCount = await CountPendingOutboxAsync(context, stationCode, SyncAggregateTypes.WeighTicket);
            PendingDeliveryTicketsCount = await CountPendingOutboxAsync(context, stationCode, SyncAggregateTypes.DeliveryTicket);
            FailedSyncCount = await context.SyncOutbox
                .Where(o => o.StationCode == stationCode && (o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.FAILED_FINAL))
                .CountAsync(CancellationToken.None);
            SuccessSyncCount = await context.SyncOutbox
                .Where(o => o.StationCode == stationCode && o.Status == OutboxStatus.SUCCESS)
                .CountAsync(CancellationToken.None);

            UnprocessedInboundCount = await context.CutOrders
                .Where(r => r.StationCode == stationCode && !r.IsInboundProcessed)
                .CountAsync(CancellationToken.None);

            var lastFailItem = await context.SyncOutbox
                .Where(o => o.StationCode == stationCode && !string.IsNullOrWhiteSpace(o.LastError))
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            var lastSuccessItem = await context.SyncOutbox
                .Where(o => o.StationCode == stationCode && o.Status == OutboxStatus.SUCCESS)
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            LastSyncError = lastFailItem?.LastError ?? "\u004b\u0068\u00f4\u006e\u0067\u0020\u0063\u00f3\u0020\u006c\u1ed7\u0069\u0020\u0067\u0068\u0069\u0020\u006e\u0068\u1ead\u006e\u002e";
            LastSyncFailureAt = FormatTimestamp(lastFailItem?.UpdatedAt ?? lastFailItem?.CreatedAt);
            LastSyncSuccessAt = FormatTimestamp(lastSuccessItem?.UpdatedAt ?? lastSuccessItem?.CreatedAt);
            SyncItems = new ObservableCollection<SyncOutboxListItem>(await LoadSyncItemsAsync(context, stationCode));

            VehicleMasterCount = await context.Vehicles.AsNoTracking().CountAsync(CancellationToken.None);
            CustomerMasterCount = await context.Customers.AsNoTracking().CountAsync(CancellationToken.None);
            ProductMasterCount = await context.Products.AsNoTracking().CountAsync(CancellationToken.None);

            var pendingMasterCount = await context.SyncOutbox.AsNoTracking()
                .Where(o => o.StationCode == stationCode
                    && MasterAggregateTypes.Contains(o.AggregateType)
                    && (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE || o.Status == OutboxStatus.PROCESSING))
                .CountAsync(CancellationToken.None);

            var lastMasterSuccess = await context.SyncOutbox.AsNoTracking()
                .Where(o => o.StationCode == stationCode && MasterAggregateTypes.Contains(o.AggregateType) && o.Status == OutboxStatus.SUCCESS)
                .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            var lastMasterFailure = await context.SyncOutbox.AsNoTracking()
                .Where(o => o.StationCode == stationCode && MasterAggregateTypes.Contains(o.AggregateType) && !string.IsNullOrWhiteSpace(o.LastError))
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
        PageNumber = 1;

        await LoadAsync();

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        await dialogService.ShowInfoAsync("\u0054\u0068\u00f4\u006e\u0067\u0020\u0062\u00e1\u006f", "\u0110\u00e3\u0020\u0063\u1ead\u0070\u0020\u006e\u0068\u1ead\u0074\u0020\u0064\u1eef\u0020\u006c\u0069\u1ec7\u0075\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u002e");
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<StationApp.Application.Interfaces.ISyncOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<StationApp.Application.Interfaces.IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<StationApp.Application.Interfaces.IClock>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var affected = await outboxRepo.ForceRetryNowAsync(clock.NowLocal, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);
        await LoadAsync();

        await dialogService.ShowInfoAsync(
            "\u0054\u0068\u00f4\u006e\u0067\u0020\u0062\u00e1\u006f",
            affected > 0
                ? $"\u0110\u00e3\u0020\u006d\u1edf\u0020\u006c\u1ea1\u0069\u0020{affected}\u0020\u0062\u1ea3\u006e\u0020\u0067\u0068\u0069\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u0020\u0111\u1ec3\u0020\u0077\u006f\u0072\u006b\u0065\u0072\u0020\u0072\u0065\u0074\u0072\u0079\u0020\u006e\u0067\u0061\u0079\u0020\u0074\u0072\u006f\u006e\u0067\u0020\u0063\u0068\u0075\u0020\u006b\u1ef3\u0020\u006b\u1ebf\u0020\u0074\u0069\u1ebf\u0070\u002e"
                : "\u004b\u0068\u00f4\u006e\u0067\u0020\u0063\u00f3\u0020\u0062\u1ea3\u006e\u0020\u0067\u0068\u0069\u0020\u006e\u00e0\u006f\u0020\u0111\u1ec3\u0020\u0111\u1ed3\u006e\u0067\u0020\u0062\u1ed9\u0020\u006c\u1ea1\u0069\u002e");
    }

    [RelayCommand(CanExecute = nameof(CanResyncSelected))]
    private async Task ResyncSelectedAsync()
    {
        if (SelectedSyncItem == null)
        {
            return;
        }

        await ResyncItemCoreAsync(SelectedSyncItem);
    }

    [RelayCommand]
    private async Task ResyncItemAsync(SyncOutboxListItem? item)
    {
        if (item == null)
        {
            return;
        }

        SelectedSyncItem = item;
        await ResyncItemCoreAsync(item);
    }

    private async Task ResyncItemCoreAsync(SyncOutboxListItem item)
    {
        SelectedSyncItem = item;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var userContext = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        try
        {
            var now = clock.NowLocal;
            var actor = string.IsNullOrWhiteSpace(userContext.Username) ? "SYSTEM_MANUAL_RESYNC" : userContext.Username;
            var payload = await PrepareAggregateForResyncAsync(context, payloadFactory, item, now, actor);
            if (payload == null)
            {
                await dialogService.ShowWarningAsync("Cảnh báo", "Không tìm thấy chứng từ tương ứng để đồng bộ lại.");
                return;
            }

            var latestOutbox = await context.SyncOutbox
                .Where(x => x.AggregateId == item.AggregateId && x.AggregateType == item.RawAggregateType)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(CancellationToken.None);

            if (latestOutbox == null || latestOutbox.Status is OutboxStatus.SUCCESS or OutboxStatus.FAILED_FINAL)
            {
                await context.SyncOutbox.AddAsync(new SyncOutbox
                {
                    Id = Guid.NewGuid(),
                    AggregateId = item.AggregateId,
                    AggregateType = item.RawAggregateType,
                    PayloadJson = payload.Value.PayloadJson,
                    IdempotencyKey = payload.Value.IdempotencyKey,
                    Status = OutboxStatus.PENDING,
                    RetryCount = 0,
                    LastError = null,
                    NextRetryAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                }, CancellationToken.None);
            }
            else
            {
                latestOutbox.PayloadJson = payload.Value.PayloadJson;
                latestOutbox.IdempotencyKey = payload.Value.IdempotencyKey;
                latestOutbox.Status = OutboxStatus.PENDING;
                latestOutbox.RetryCount = 0;
                latestOutbox.LastError = null;
                latestOutbox.NextRetryAt = now;
                latestOutbox.UpdatedAt = now;
            }

            await context.SaveChangesAsync(CancellationToken.None);
            await LoadAsync();
            await dialogService.ShowInfoAsync("Thông báo", "Đã đưa chứng từ đã chọn vào hàng đợi đồng bộ lại.");
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi đồng bộ lại chứng từ: {ex.Message}");
        }
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

    private bool CanResyncSelected() => SelectedSyncItem != null;

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (PageNumber <= 1)
        {
            return;
        }

        PageNumber--;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoToNextPage)
        {
            return;
        }

        PageNumber++;
        await LoadAsync();
    }

    private static Task<int> CountPendingOutboxAsync(StationDbContext context, string stationCode, string aggregateType)
    {
        return context.SyncOutbox
            .Where(o => o.StationCode == stationCode &&
                        o.AggregateType == aggregateType &&
                        (o.Status == OutboxStatus.PENDING || o.Status == OutboxStatus.FAILED_RETRYABLE))
            .CountAsync(CancellationToken.None);
    }

    partial void OnSelectedAggregateTypeChanged(string value)
    {
        PageNumber = 1;
        _ = LoadAsync();
    }

    partial void OnSelectedOutboxStatusChanged(string value)
    {
        PageNumber = 1;
        _ = LoadAsync();
    }

    partial void OnSearchKeywordChanged(string? value)
    {
        PageNumber = 1;
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        _ = DebouncedLoadAsync(token);
    }
    partial void OnSelectedSyncItemChanged(SyncOutboxListItem? value) => ResyncSelectedCommand.NotifyCanExecuteChanged();

    private async Task DebouncedLoadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                await LoadAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string FormatTimestamp(DateTime? timestamp)
    {
        return timestamp?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
    }

    private async Task<IReadOnlyList<SyncOutboxListItem>> LoadSyncItemsAsync(StationDbContext context, string stationCode)
    {
        var query = context.SyncOutbox.AsNoTracking()
            .Where(x => x.StationCode == stationCode);

        if (SelectedAggregateType != "\u0054\u1ea5\u0074\u0020\u0063\u1ea3")
        {
            var aggregateType = SelectedAggregateType switch
            {
                "\u0110\u004b\u0050\u0054" => SyncAggregateTypes.CutOrder,
                "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e" => SyncAggregateTypes.WeighTicket,
                "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e" => SyncAggregateTypes.DeliveryTicket,
                "\u0050\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e" => SyncAggregateTypes.WeighingSession,
                "\u0044\u00f2\u006e\u0067\u0020\u0070\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e" => SyncAggregateTypes.WeighingSessionLine,
                "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0074\u0072\u1ea1\u006d" => SyncAggregateTypes.Station,
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

        query = ApplySearchFilter(context, query, SearchKeyword);

        TotalSyncItemsCount = await query.CountAsync(CancellationToken.None);
        var pageSize = Math.Max(20, PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalSyncItemsCount / (double)pageSize));
        if (PageNumber > totalPages)
        {
            PageNumber = totalPages;
        }

        var skip = (PageNumber - 1) * pageSize;
        var outboxItems = await query
            .OrderBy(x => x.Status == OutboxStatus.PENDING ? 0 : x.Status == OutboxStatus.FAILED_RETRYABLE ? 1 : x.Status == OutboxStatus.PROCESSING ? 2 : 3)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(CancellationToken.None);

        CanGoToPreviousPage = PageNumber > 1;
        CanGoToNextPage = PageNumber < totalPages;
        PageSummary = TotalSyncItemsCount == 0
            ? "Không có dữ liệu"
            : $"Hiển thị {skip + 1:N0}-{skip + outboxItems.Count:N0} / {TotalSyncItemsCount:N0} bản ghi";

        var registrationIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.CutOrder).Select(x => x.AggregateId).Distinct().ToList();
        var weighTicketIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.WeighTicket).Select(x => x.AggregateId).Distinct().ToList();
        var deliveryTicketIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.DeliveryTicket).Select(x => x.AggregateId).Distinct().ToList();
        var directSessionIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.WeighingSession).Select(x => x.AggregateId).Distinct().ToList();
        var lineIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.WeighingSessionLine).Select(x => x.AggregateId).Distinct().ToList();
        var stationIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Station).Select(x => x.AggregateId).Distinct().ToList();
        var vehicleIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Vehicle).Select(x => x.AggregateId).Distinct().ToList();
        var customerIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Customer).Select(x => x.AggregateId).Distinct().ToList();
        var productIds = outboxItems.Where(x => x.AggregateType == SyncAggregateTypes.Product).Select(x => x.AggregateId).Distinct().ToList();

        var registrations = await context.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && registrationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var weighTickets = await context.WeighTickets.AsNoTracking()
            .Where(x => x.StationCode == stationCode && weighTicketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var deliveryTickets = await context.DeliveryTickets.AsNoTracking()
            .Where(x => x.StationCode == stationCode && deliveryTicketIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var stations = await context.Stations.AsNoTracking()
            .Where(x => stationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);
        var lines = await context.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && lineIds.Contains(x.Id))
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
            .Concat(directSessionIds)
            .Concat(lines.Values.Select(x => x.WeighingSessionId))
            .Distinct()
            .ToList();

        var sessions = await context.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && sessionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, CancellationToken.None);

        var items = outboxItems.Select(item =>
        {
            var aggregateDisplay = GetAggregateTypeDisplay(item.AggregateType);
            var businessNo = string.Empty;
            var sessionNo = string.Empty;
            var vehiclePlate = string.Empty;
            var entitySyncStatus = "-";

            if (item.AggregateType == SyncAggregateTypes.CutOrder && registrations.TryGetValue(item.AggregateId, out var registration))
            {
                businessNo = registration.ErpCutOrderId
                    ?? registration.OrderCode
                    ?? registration.VehiclePlate
                    ?? registration.Id.ToString("N")[..8];
                vehiclePlate = registration.VehiclePlate ?? string.Empty;
                entitySyncStatus = registration.SyncStatus.ToString();
                if (registration.WeighingSessionId.HasValue && sessions.TryGetValue(registration.WeighingSessionId.Value, out var registrationSession))
                {
                    sessionNo = BusinessNumberFormatter.ToDisplay(registrationSession.SessionNo);
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.WeighTicket && weighTickets.TryGetValue(item.AggregateId, out var weighTicket))
            {
                businessNo = BusinessNumberFormatter.ToDisplay(weighTicket.TicketNo);
                vehiclePlate = weighTicket.VehiclePlate;
                entitySyncStatus = weighTicket.SyncStatus.ToString();
                if (weighTicket.WeighingSessionId.HasValue && sessions.TryGetValue(weighTicket.WeighingSessionId.Value, out var ticketSession))
                {
                    sessionNo = BusinessNumberFormatter.ToDisplay(ticketSession.SessionNo);
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.DeliveryTicket && deliveryTickets.TryGetValue(item.AggregateId, out var deliveryTicket))
            {
                businessNo = BusinessNumberFormatter.ToDisplay(deliveryTicket.DeliveryNo);
                entitySyncStatus = deliveryTicket.SyncStatus.ToString();
                if (deliveryTicket.WeighingSessionId.HasValue && sessions.TryGetValue(deliveryTicket.WeighingSessionId.Value, out var deliverySession))
                {
                    sessionNo = BusinessNumberFormatter.ToDisplay(deliverySession.SessionNo);
                    vehiclePlate = deliverySession.VehiclePlate;
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.WeighingSession && sessions.TryGetValue(item.AggregateId, out var session))
            {
                businessNo = BusinessNumberFormatter.ToDisplay(session.SessionNo);
                sessionNo = BusinessNumberFormatter.ToDisplay(session.SessionNo);
                vehiclePlate = session.VehiclePlate;
                entitySyncStatus = session.SyncStatus.ToString();
            }
            else if (item.AggregateType == SyncAggregateTypes.WeighingSessionLine && lines.TryGetValue(item.AggregateId, out var line))
            {
                businessNo = $"Line {line.SequenceNo}";
                entitySyncStatus = line.SyncStatus.ToString();
                if (sessions.TryGetValue(line.WeighingSessionId, out var lineSession))
                {
                    sessionNo = BusinessNumberFormatter.ToDisplay(lineSession.SessionNo);
                    vehiclePlate = lineSession.VehiclePlate;
                }
            }
            else if (item.AggregateType == SyncAggregateTypes.Station && stations.TryGetValue(item.AggregateId, out var station))
            {
                businessNo = station.StationCode;
                entitySyncStatus = station.IsActive ? "ACTIVE" : "INACTIVE";
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
                item.AggregateId,
                item.AggregateType,
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

        return items.ToList();
    }

    private static IQueryable<SyncOutbox> ApplySearchFilter(StationDbContext context, IQueryable<SyncOutbox> query, string? searchKeyword)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            return query;
        }

        var keyword = searchKeyword.Trim();
        return query.Where(o =>
            o.AggregateType.Contains(keyword)
            || (o.LastError != null && o.LastError.Contains(keyword))
            || (o.AggregateType == SyncAggregateTypes.CutOrder && context.CutOrders.Any(x =>
                x.Id == o.AggregateId
                && ((x.ErpCutOrderId != null && x.ErpCutOrderId.Contains(keyword))
                    || (x.OrderCode != null && x.OrderCode.Contains(keyword))
                    || x.VehiclePlate.Contains(keyword)
                    || (x.CustomerName != null && x.CustomerName.Contains(keyword))
                    || (x.ProductName != null && x.ProductName.Contains(keyword)))))
            || (o.AggregateType == SyncAggregateTypes.WeighTicket && context.WeighTickets.Any(x =>
                x.Id == o.AggregateId
                && (x.TicketNo.Contains(keyword)
                    || x.VehiclePlate.Contains(keyword)
                    || (x.CustomerName != null && x.CustomerName.Contains(keyword))
                    || (x.ProductName != null && x.ProductName.Contains(keyword)))))
            || (o.AggregateType == SyncAggregateTypes.DeliveryTicket && context.DeliveryTickets.Any(x =>
                x.Id == o.AggregateId
                && (x.DeliveryNo.Contains(keyword)
                    || x.ErpCutOrderId.Contains(keyword)
                    || (x.CustomerCode != null && x.CustomerCode.Contains(keyword))
                    || (x.ProductCode != null && x.ProductCode.Contains(keyword))
                    || (x.Notes != null && x.Notes.Contains(keyword)))))
            || (o.AggregateType == SyncAggregateTypes.WeighingSession && context.WeighingSessions.Any(x =>
                x.Id == o.AggregateId
                && (x.SessionNo.Contains(keyword)
                    || x.VehiclePlate.Contains(keyword)
                    || (x.MoocNumber != null && x.MoocNumber.Contains(keyword))
                    || (x.DriverName != null && x.DriverName.Contains(keyword)))))
            || (o.AggregateType == SyncAggregateTypes.WeighingSessionLine && context.WeighingSessionLines.Any(line =>
                line.Id == o.AggregateId
                && ((line.CustomerName != null && line.CustomerName.Contains(keyword))
                    || (line.CustomerCode != null && line.CustomerCode.Contains(keyword))
                    || (line.ProductName != null && line.ProductName.Contains(keyword))
                    || (line.ProductCode != null && line.ProductCode.Contains(keyword))
                    || context.CutOrders.Any(cutOrder =>
                        cutOrder.Id == line.CutOrderId
                        && ((cutOrder.ErpCutOrderId != null && cutOrder.ErpCutOrderId.Contains(keyword))
                            || cutOrder.VehiclePlate.Contains(keyword)))
                    || context.WeighingSessions.Any(session =>
                        session.Id == line.WeighingSessionId
                        && (session.SessionNo.Contains(keyword) || session.VehiclePlate.Contains(keyword))))))
            || (o.AggregateType == SyncAggregateTypes.Station && context.Stations.Any(x =>
                x.Id == o.AggregateId
                && (x.StationCode.Contains(keyword) || x.StationName.Contains(keyword))))
            || (o.AggregateType == SyncAggregateTypes.Vehicle && context.Vehicles.Any(x =>
                x.Id == o.AggregateId
                && (x.VehiclePlate.Contains(keyword)
                    || x.MoocNumber.Contains(keyword)
                    || (x.DriverName != null && x.DriverName.Contains(keyword)))))
            || (o.AggregateType == SyncAggregateTypes.Customer && context.Customers.Any(x =>
                x.Id == o.AggregateId
                && (x.CustomerCode.Contains(keyword) || x.CustomerName.Contains(keyword))))
            || (o.AggregateType == SyncAggregateTypes.Product && context.Products.Any(x =>
                x.Id == o.AggregateId
                && (x.ProductCode.Contains(keyword) || x.ProductName.Contains(keyword)))));
    }

    private static async Task<(string PayloadJson, Guid IdempotencyKey)?> PrepareAggregateForResyncAsync(
        StationDbContext context,
        ISyncPayloadFactory payloadFactory,
        SyncOutboxListItem item,
        DateTime now,
        string actor)
    {
        switch (item.RawAggregateType)
        {
            case SyncAggregateTypes.CutOrder:
            {
                var registration = await context.CutOrders.FindAsync([item.AggregateId], CancellationToken.None);
                if (registration == null) return null;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
                registration.LastSyncAttemptAt = null;
                registration.LastSyncError = null;
                registration.UpdatedAt = now;
                registration.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(registration), registration.IdempotencyKey);
            }
            case SyncAggregateTypes.WeighTicket:
            {
                var weighTicket = await context.WeighTickets.FindAsync([item.AggregateId], CancellationToken.None);
                if (weighTicket == null) return null;
                weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
                weighTicket.UpdatedAt = now;
                weighTicket.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(weighTicket), weighTicket.IdempotencyKey);
            }
            case SyncAggregateTypes.DeliveryTicket:
            {
                var deliveryTicket = await context.DeliveryTickets.FindAsync([item.AggregateId], CancellationToken.None);
                if (deliveryTicket == null) return null;
                deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
                deliveryTicket.UpdatedAt = now;
                deliveryTicket.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(deliveryTicket), deliveryTicket.Id);
            }
            case SyncAggregateTypes.WeighingSession:
            {
                var session = await context.WeighingSessions.FindAsync([item.AggregateId], CancellationToken.None);
                if (session == null) return null;
                session.SyncStatus = SyncStatus.SYNC_QUEUED;
                session.LastSyncAttemptAt = null;
                session.LastSyncError = null;
                session.UpdatedAt = now;
                session.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(session), session.Id);
            }
            case SyncAggregateTypes.WeighingSessionLine:
            {
                var line = await context.WeighingSessionLines.FindAsync([item.AggregateId], CancellationToken.None);
                if (line == null) return null;
                line.SyncStatus = SyncStatus.SYNC_QUEUED;
                line.LastSyncAttemptAt = null;
                line.LastSyncError = null;
                line.UpdatedAt = now;
                line.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(line), line.Id);
            }
            case SyncAggregateTypes.Station:
            {
                var station = await context.Stations.FindAsync([item.AggregateId], CancellationToken.None);
                if (station == null) return null;

                station.UpdatedAt = now;
                station.UpdatedBy = actor;

                var featureFlags = await context.StationFeatureFlags.AsNoTracking()
                    .Where(x => x.StationCode == station.StationCode)
                    .OrderBy(x => x.FeatureKey)
                    .Select(x => new SyncStationFeatureFlagItem
                    {
                        FeatureKey = x.FeatureKey,
                        FeatureValue = x.FeatureValue
                    })
                    .ToListAsync(CancellationToken.None);

                var operationSettings = await context.StationOperationSettings.AsNoTracking()
                    .Where(x => x.StationCode == station.StationCode)
                    .OrderBy(x => x.SettingKey)
                    .Select(x => new SyncStationOperationSettingItem
                    {
                        SettingKey = x.SettingKey,
                        SettingValue = x.SettingValue
                    })
                    .ToListAsync(CancellationToken.None);

                var payload = new SyncStationMasterDataRequest
                {
                    Id = station.Id,
                    StationCode = station.StationCode,
                    StationName = station.StationName,
                    IsActive = station.IsActive,
                    SortOrder = station.SortOrder,
                    CreatedAt = station.CreatedAt,
                    CreatedBy = station.CreatedBy,
                    UpdatedAt = station.UpdatedAt,
                    UpdatedBy = station.UpdatedBy,
                    FeatureFlags = featureFlags,
                    OperationSettings = operationSettings
                };

                return (payloadFactory.CreatePayload(payload), station.Id);
            }
            case SyncAggregateTypes.Vehicle:
            {
                var vehicle = await context.Vehicles.FindAsync([item.AggregateId], CancellationToken.None);
                if (vehicle == null) return null;
                vehicle.UpdatedAt = now;
                vehicle.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(vehicle), vehicle.Id);
            }
            case SyncAggregateTypes.Customer:
            {
                var customer = await context.Customers.FindAsync([item.AggregateId], CancellationToken.None);
                if (customer == null) return null;
                customer.UpdatedAt = now;
                customer.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(customer), customer.Id);
            }
            case SyncAggregateTypes.Product:
            {
                var product = await context.Products.FindAsync([item.AggregateId], CancellationToken.None);
                if (product == null) return null;
                product.UpdatedAt = now;
                product.UpdatedBy = actor;
                return (payloadFactory.CreatePayload(product), product.Id);
            }
            default:
                return null;
        }
    }

    private static string GetAggregateTypeDisplay(string aggregateType)
    {
        return aggregateType switch
        {
            SyncAggregateTypes.CutOrder => "\u0110\u004b\u0050\u0054",
            SyncAggregateTypes.WeighTicket => "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0063\u00e2\u006e",
            SyncAggregateTypes.DeliveryTicket => "\u0050\u0068\u0069\u1ebf\u0075\u0020\u0067\u0069\u0061\u006f\u0020\u006e\u0068\u1ead\u006e",
            SyncAggregateTypes.WeighingSession => "\u0050\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e",
            SyncAggregateTypes.WeighingSessionLine => "\u0044\u00f2\u006e\u0067\u0020\u0070\u0068\u0069\u00ea\u006e\u0020\u0063\u00e2\u006e",
            SyncAggregateTypes.Station => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0074\u0072\u1ea1\u006d",
            SyncAggregateTypes.Vehicle => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0078\u0065",
            SyncAggregateTypes.Customer => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u006b\u0068\u00e1\u0063\u0068\u0020\u0068\u00e0\u006e\u0067",
            SyncAggregateTypes.Product => "\u0044\u0061\u006e\u0068\u0020\u006d\u1ee5\u0063\u0020\u0073\u1ea3\u006e\u0020\u0070\u0068\u1ea9\u006d",
            _ => aggregateType
        };
    }
}

public sealed record SyncOutboxListItem(
    Guid OutboxId,
    Guid AggregateId,
    string RawAggregateType,
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

