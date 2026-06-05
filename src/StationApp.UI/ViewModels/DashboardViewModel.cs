using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private sealed record SessionDashboardSnapshot(WeighingSession Session, bool IsExportScale);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private bool _isInitialized;

    [ObservableProperty] private DateTime? _selectedDate = DateTime.Today;

    [ObservableProperty] private int _inboundWaitingCount;
    [ObservableProperty] private int _inboundProcessingCount;
    [ObservableProperty] private int _inboundCompletedCount;
    [ObservableProperty] private decimal _inboundCompletedTonnage;

    [ObservableProperty] private int _outboundWaitingCount;
    [ObservableProperty] private int _outboundProcessingCount;
    [ObservableProperty] private int _outboundCompletedCount;
    [ObservableProperty] private decimal _outboundCompletedTonnage;

    [ObservableProperty] private string _networkStatus = "Đang kiểm tra...";
    [ObservableProperty] private SolidColorBrush _networkStatusBrush = new(Colors.Gray);
    [ObservableProperty] private string _deviceStatus = "Đang kiểm tra...";
    [ObservableProperty] private SolidColorBrush _deviceStatusBrush = new(Colors.Gray);
    [ObservableProperty] private string? _lastSyncTime;
    [ObservableProperty] private string? _stationCode;

    public DashboardViewModel(IServiceScopeFactory scopeFactory, IScaleDevice scaleDevice)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
    }

    public async Task InitializeAsync()
    {
        await RefreshDashboardAsync();
        _isInitialized = true;
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _ = RefreshDashboardAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshDashboardAsync();
    }

    private async Task RefreshDashboardAsync()
    {
        ResetKpiState();
        await LoadCountersAsync();
        CheckDeviceStatus();
        await CheckNetworkStatusAsync();
    }

    private void ResetKpiState()
    {
        InboundWaitingCount = 0;
        InboundProcessingCount = 0;
        InboundCompletedCount = 0;
        InboundCompletedTonnage = 0m;
        OutboundWaitingCount = 0;
        OutboundProcessingCount = 0;
        OutboundCompletedCount = 0;
        OutboundCompletedTonnage = 0m;
        NetworkStatus = "Đang kiểm tra...";
        NetworkStatusBrush = new SolidColorBrush(Colors.Gray);
        DeviceStatus = "Đang kiểm tra...";
        DeviceStatusBrush = new SolidColorBrush(Colors.Gray);
        LastSyncTime = null;
        StationCode = null;
    }

    private async Task LoadCountersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        var selectedDate = (SelectedDate ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);

        var sessionCandidates = await dbContext.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .Where(x =>
                (x.CreatedAt >= selectedDate && x.CreatedAt < nextDate)
                || ((x.Weight2Time ?? x.UpdatedAt ?? x.CreatedAt) >= selectedDate
                    && (x.Weight2Time ?? x.UpdatedAt ?? x.CreatedAt) < nextDate))
            .ToListAsync(CancellationToken.None);

        var sessionIds = sessionCandidates.Select(x => x.Id).Distinct().ToList();
        var exportSessionIds = sessionIds.Count == 0
            ? new HashSet<Guid>()
            : (await (
                from line in dbContext.WeighingSessionLines.AsNoTracking()
                join cutOrder in dbContext.CutOrders.AsNoTracking()
                    on line.CutOrderId equals cutOrder.Id
                where sessionIds.Contains(line.WeighingSessionId)
                    && !line.IsDeleted
                    && !cutOrder.IsDeleted
                    && cutOrder.IsExportScale
                select line.WeighingSessionId)
                .Distinct()
                .ToListAsync(CancellationToken.None))
            .ToHashSet();

        var sessions = sessionCandidates
            .Select(x => new SessionDashboardSnapshot(x, exportSessionIds.Contains(x.Id)))
            .ToList();

        ApplyKpiMetrics(
            TransactionType.INBOUND,
            sessions,
            selectedDate,
            nextDate,
            waitingSetter: value => InboundWaitingCount = value,
            processingSetter: value => InboundProcessingCount = value,
            completedSetter: value => InboundCompletedCount = value,
            tonnageSetter: value => InboundCompletedTonnage = value);

        ApplyKpiMetrics(
            TransactionType.OUTBOUND,
            sessions,
            selectedDate,
            nextDate,
            waitingSetter: value => OutboundWaitingCount = value,
            processingSetter: value => OutboundProcessingCount = value,
            completedSetter: value => OutboundCompletedCount = value,
            tonnageSetter: value => OutboundCompletedTonnage = value);

        var completedOutboundSessions = sessions
            .Where(x => x.Session.TransactionType == TransactionType.OUTBOUND)
            .Where(IsCompletedForDashboard)
            .Where(x => IsInSelectedDate(ResolveCompletedAt(x), selectedDate, nextDate))
            .Where(x => !x.Session.IsNoLoad)
            .ToList();

        OutboundCompletedTonnage = await CalculateOutboundCompletedTonnageAsync(
            dbContext,
            completedOutboundSessions,
            CancellationToken.None);

        var lastMasterSuccess = await dbContext.SyncOutbox.AsNoTracking()
            .Where(x =>
                (x.AggregateType == SyncAggregateTypes.Vehicle
                 || x.AggregateType == SyncAggregateTypes.Customer
                 || x.AggregateType == SyncAggregateTypes.Product
                 || x.AggregateType == SyncAggregateTypes.WeighingSession
                 || x.AggregateType == SyncAggregateTypes.WeighingSessionLine
                 || x.AggregateType == SyncAggregateTypes.WeighTicket
                 || x.AggregateType == SyncAggregateTypes.DeliveryTicket
                 || x.AggregateType == SyncAggregateTypes.CutOrder)
                && x.Status == OutboxStatus.SUCCESS)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);

        StationCode = await appConfig.GetValueAsync("station_code", CancellationToken.None) ?? "N/A";
        LastSyncTime = lastMasterSuccess == null
            ? "Chưa đồng bộ"
            : (lastMasterSuccess.UpdatedAt ?? lastMasterSuccess.CreatedAt).ToString("dd/MM/yyyy HH:mm:ss");
    }

    private static void ApplyKpiMetrics(
        TransactionType transactionType,
        IReadOnlyCollection<SessionDashboardSnapshot> sessions,
        DateTime selectedDate,
        DateTime nextDate,
        Action<int> waitingSetter,
        Action<int> processingSetter,
        Action<int> completedSetter,
        Action<decimal> tonnageSetter)
    {
        var sessionsByType = sessions.Where(x => x.Session.TransactionType == transactionType).ToList();
        var completedByType = sessionsByType
            .Where(IsCompletedForDashboard)
            .Where(x => IsInSelectedDate(ResolveCompletedAt(x), selectedDate, nextDate))
            .ToList();

        waitingSetter(sessionsByType.Count(x =>
            IsInSelectedDate(x.Session.CreatedAt, selectedDate, nextDate)
            && x.Session.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1));

        processingSetter(sessionsByType.Count(x =>
            IsInSelectedDate(x.Session.CreatedAt, selectedDate, nextDate)
            && IsProcessingForDashboard(x)));

        completedSetter(completedByType.Count);
        tonnageSetter(decimal.Round(
            completedByType
                .Where(x => !x.Session.IsNoLoad)
                .Sum(x => (x.Session.NetWeight ?? 0m) / 1000m),
            3,
            MidpointRounding.AwayFromZero));
    }

    private static bool IsProcessingForDashboard(SessionDashboardSnapshot snapshot)
    {
        if (snapshot.Session.TransactionType == TransactionType.OUTBOUND && snapshot.IsExportScale)
        {
            return snapshot.Session.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING;
        }

        return snapshot.Session.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT2
            or WeighingSessionStatus.ALLOCATION_PENDING
            or WeighingSessionStatus.READY_TO_COMPLETE;
    }

    private static bool IsCompletedForDashboard(SessionDashboardSnapshot snapshot)
    {
        if (snapshot.Session.TransactionType == TransactionType.OUTBOUND && snapshot.IsExportScale)
        {
            return snapshot.Session.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE
                or WeighingSessionStatus.COMPLETED;
        }

        return snapshot.Session.SessionStatus == WeighingSessionStatus.COMPLETED;
    }

    private static DateTime? ResolveCompletedAt(SessionDashboardSnapshot snapshot)
    {
        if (!IsCompletedForDashboard(snapshot))
        {
            return null;
        }

        return snapshot.Session.Weight2Time
            ?? snapshot.Session.UpdatedAt
            ?? snapshot.Session.CreatedAt;
    }

    private static bool IsInSelectedDate(DateTime? value, DateTime selectedDate, DateTime nextDate)
        => value.HasValue && value.Value >= selectedDate && value.Value < nextDate;

    private static async Task<decimal> CalculateOutboundCompletedTonnageAsync(
        StationDbContext dbContext,
        IReadOnlyCollection<SessionDashboardSnapshot> completedOutboundSessions,
        CancellationToken ct)
    {
        if (completedOutboundSessions.Count == 0)
        {
            return 0m;
        }

        var completedSessionIds = completedOutboundSessions
            .Select(x => x.Session.Id)
            .ToHashSet();

        var lineWeights = await dbContext.WeighingSessionLines.AsNoTracking()
            .Where(x => completedSessionIds.Contains(x.WeighingSessionId) && !x.IsDeleted)
            .Select(x => new
            {
                x.WeighingSessionId,
                x.ActualAllocatedWeight
            })
            .ToListAsync(ct);

        var lineWeightBySessionId = lineWeights
            .GroupBy(x => x.WeighingSessionId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => y.ActualAllocatedWeight ?? 0m));

        var totalKg = completedOutboundSessions.Sum(x =>
        {
            var lineWeight = lineWeightBySessionId.GetValueOrDefault(x.Session.Id);
            return lineWeight > 0m
                ? lineWeight
                : x.Session.NetWeight ?? 0m;
        });

        return decimal.Round(totalKg / 1000m, 3, MidpointRounding.AwayFromZero);
    }

    private void CheckDeviceStatus()
    {
        if (_scaleDevice.IsConnected)
        {
            DeviceStatus = "Đang hoạt động";
            DeviceStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
        }
        else
        {
            DeviceStatus = "Mất kết nối";
            DeviceStatusBrush = new SolidColorBrush(Colors.Red);
        }
    }

    private async Task CheckNetworkStatusAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var checker = scope.ServiceProvider.GetRequiredService<ICentralApiHealthChecker>();
        var result = await checker.CheckAsync(CancellationToken.None);
        if (result.Success)
        {
            NetworkStatus = "Kết nối OK";
            NetworkStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
            return;
        }

        NetworkStatus = result.StatusCode == "CONFIG_INVALID"
            ? "Chưa cấu hình"
            : result.Message;
        NetworkStatusBrush = result.StatusCode == "CONFIG_INVALID"
            ? new SolidColorBrush(Colors.Orange)
            : new SolidColorBrush(Colors.Red);
    }
}
