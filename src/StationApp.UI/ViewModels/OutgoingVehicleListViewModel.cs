using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class OutgoingVehicleListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;
    private readonly ILogger<OutgoingVehicleListViewModel>? _logger;

    [ObservableProperty] private ObservableCollection<OutgoingVehicleListItem> _vehicles = new();
    [ObservableProperty] private OutgoingVehicleListItem? _selectedVehicle;
    [ObservableProperty] private string? _searchErpVehicleRegistrationId;
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private bool _isLoading;

    public OutgoingVehicleListViewModel(IServiceScopeFactory scopeFactory, IToastService toastService, ILogger<OutgoingVehicleListViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _toastService = toastService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
            var list = await repo.GetOutgoingListAsync(
                new OutgoingVehicleListFilter(
                    SearchErpVehicleRegistrationId,
                    SearchVehiclePlate,
                    null,
                    null,
                    null),
                CancellationToken.None);

            Vehicles = new ObservableCollection<OutgoingVehicleListItem>(list);

            if (list.Count == 0 && HasSearchFilters())
            {
                _toastService.ShowInfo("Khong tim thay du lieu phu hop.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadOutgoingVehicles failed");
            _toastService.ShowError("Khong the tai danh sach xe ra. Vui long thu lai.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadVehiclesAsync();
    }

    public async Task InitializeAsync()
    {
        await LoadVehiclesAsync();
    }

    private bool HasSearchFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchErpVehicleRegistrationId)
            || !string.IsNullOrWhiteSpace(SearchVehiclePlate);
    }
}
