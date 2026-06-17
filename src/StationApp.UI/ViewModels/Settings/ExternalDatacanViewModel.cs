using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.UI.ViewModels.Settings;

public partial class ExternalDatacanViewModel : ObservableObject
{
    private const int DefaultPageSize = 100;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExternalDatacanViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public List<string> Sources { get; } = new() { "Trạm cân NMC", "Trạm đập" };
    [ObservableProperty] private string _selectedSource = "Trạm cân NMC";

    [ObservableProperty] private ObservableCollection<ExternalDatacanRecordDto> _records = new();
    [ObservableProperty] private string? _vehiclePlateKeyword;
    [ObservableProperty] private string? _productKeyword;
    [ObservableProperty] private string? _customerKeyword;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private string _pageSummary = "Không có dữ liệu";

    public bool CanGoToPreviousPage => PageIndex > 0 && !IsLoading;
    public bool CanGoToNextPage => HasNextPage && !IsLoading;

    partial void OnPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        UpdatePageSummary();
    }

    partial void OnHasNextPageChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoToNextPage));
        UpdatePageSummary();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        RefreshCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        _ = LoadPageAsync(resetPage: true);
    }

    public async Task LoadAsync()
    {
        await LoadPageAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task RefreshAsync()
    {
        await LoadPageAsync(resetPage: true);
    }

    [RelayCommand(CanExecute = nameof(CanRunQuery))]
    private async Task SearchAsync()
    {
        await LoadPageAsync(resetPage: true);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private async Task PreviousPageAsync()
    {
        if (PageIndex <= 0)
        {
            return;
        }

        PageIndex--;
        await LoadPageAsync(resetPage: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private async Task NextPageAsync()
    {
        if (!HasNextPage)
        {
            return;
        }

        PageIndex++;
        await LoadPageAsync(resetPage: false);
    }

    private bool CanRunQuery() => !IsLoading;

    private async Task LoadPageAsync(bool resetPage)
    {
        if (IsLoading)
        {
            return;
        }

        if (resetPage)
        {
            PageIndex = 0;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IExternalDatacanQueryService>();
            var result = await service.GetLatestAsync(
                SelectedSource,
                VehiclePlateKeyword,
                ProductKeyword,
                CustomerKeyword,
                PageIndex,
                PageSize,
                CancellationToken.None);

            Records = new ObservableCollection<ExternalDatacanRecordDto>(result.Records);
            HasNextPage = result.HasNextPage;
            UpdatePageSummary();
        }
        catch (Exception ex)
        {
            Records = new ObservableCollection<ExternalDatacanRecordDto>();
            HasNextPage = false;
            ErrorMessage = $"Không thể tải dữ liệu Lịch sử cân (PM cũ): {ex.Message}";
            UpdatePageSummary();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdatePageSummary()
    {
        PageSummary = Records.Count == 0
            ? "Không có dữ liệu"
            : $"Trang {PageIndex + 1} - {Records.Count} dòng";
    }
}
