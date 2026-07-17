using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public sealed partial class WeighingSessionEditHistoryViewModel : ObservableObject
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWeighingSessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty] private string _title = "L\u1ecbch s\u1eed ch\u1ec9nh s\u1eeda";
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private string? _searchKeyword;
    [ObservableProperty] private AuditActionFilterOption? _selectedActionOption;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<AuditHistoryRow> _historyItems = new();

    public ObservableCollection<AuditActionFilterOption> ActionOptions { get; } = new();

    public WeighingSessionEditHistoryViewModel(
        IAuditLogRepository auditLogRepository,
        IWeighingSessionRepository sessionRepository,
        IUserRepository userRepository,
        IToastService toastService,
        ICurrentUserContext currentUserContext)
    {
        _auditLogRepository = auditLogRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _toastService = toastService;
        _currentUserContext = currentUserContext;

        LoadActionOptions();
    }

    public async Task InitializeAsync()
    {
        await SearchAsync();
    }

    public void SetFilter(string? vehiclePlate, string? sessionNo)
    {
        SearchVehiclePlate = vehiclePlate;
        SearchSessionNo = sessionNo;
        FromDate = DateTime.Today.AddDays(-30);
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        IsLoading = true;
        HistoryItems.Clear();

        try
        {
            var logs = await _auditLogRepository.SearchAsync(
                new AuditLogSearchRequest(
                    FromDate,
                    ToDate,
                    _currentUserContext.StationCode,
                    SearchVehiclePlate,
                    SearchSessionNo,
                    SelectedActionOption?.ActionCode,
                    SearchKeyword),
                CancellationToken.None);

            var rows = new List<AuditHistoryRow>();
            var index = 1;
            foreach (var log in logs)
            {
                var fallbackDisplay = await ResolveFallbackEntityDisplayAsync(log.EntityType, log.EntityId);
                var row = AuditLogDisplayMapper.Map(log, fallbackDisplay);
                row.Index = index++;
                rows.Add(row);
            }

            HistoryItems = new ObservableCollection<AuditHistoryRow>(rows);
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Kh\u00f4ng th\u1ec3 t\u1ea3i l\u1ecbch s\u1eed ch\u1ec9nh s\u1eeda: {ex.Message}");
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
        SearchKeyword = null;
        SelectedActionOption = ActionOptions.FirstOrDefault();
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Today;
        await SearchAsync();
    }

    private void LoadActionOptions()
    {
        ActionOptions.Clear();
        ActionOptions.Add(new AuditActionFilterOption(null, "T\u1ea5t c\u1ea3"));

        foreach (var action in AuditLogDisplayMapper.KnownActions)
        {
            ActionOptions.Add(new AuditActionFilterOption(action, AuditLogDisplayMapper.ToActionDisplay(action)));
        }

        SelectedActionOption = ActionOptions.FirstOrDefault();
    }

    private async Task<string?> ResolveFallbackEntityDisplayAsync(string entityType, Guid entityId)
    {
        if (!string.Equals(entityType, "WeighingSession", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveUserEntityDisplayAsync(entityType, entityId);
        }

        try
        {
            var session = await _sessionRepository.GetByIdAsync(entityId, CancellationToken.None);
            return session?.SessionNo;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ResolveUserEntityDisplayAsync(string entityType, Guid entityId)
    {
        if (!string.Equals(entityType, "User", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entityType, "UserStationAssignment", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var user = await _userRepository.GetByIdAsync(entityId, CancellationToken.None);
            if (user == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Username
                : $"{user.Username} - {user.DisplayName}";
        }
        catch
        {
            return null;
        }
    }
}

public sealed record AuditActionFilterOption(string? ActionCode, string DisplayText)
{
    public override string ToString() => DisplayText;
}
