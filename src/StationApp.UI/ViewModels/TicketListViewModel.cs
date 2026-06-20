using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Enums;

namespace StationApp.UI.ViewModels;

public partial class TicketListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty] private ObservableCollection<TicketListItemDto> _tickets = new();
    [ObservableProperty] private TicketListItemDto? _selectedTicket;
    [ObservableProperty] private string? _searchKeyword;

    public TicketListViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    [RelayCommand]
    public async Task LoadTicketsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var list = await repo.SearchAsync(SearchKeyword, null, CancellationToken.None);
        Tickets = new ObservableCollection<TicketListItemDto>(
            list.Select(t => new TicketListItemDto(t.Id, BusinessNumberFormatter.ToDisplay(t.TicketNo), t.VehiclePlate, t.Status, t.SyncStatus,
                t.TransactionType, t.Weight1, t.Weight2, t.NetWeight, t.CreatedAt)));
    }

    [RelayCommand]
    private Task SearchAsync() => LoadTicketsAsync();

    [RelayCommand]
    private async Task CreateTicketAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var uc = scope.ServiceProvider.GetRequiredService<CreateTicketUseCase>();
        await uc.ExecuteAsync(new CreateTicketRequest(
            VehiclePlate: "TEST-001",
            TransactionType: TransactionType.OUTBOUND
        ), CancellationToken.None);
        await LoadTicketsAsync();
    }
}
