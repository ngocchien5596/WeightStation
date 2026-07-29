using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public sealed record IncomingSeedVehicleMasterOption(string Code, string Name)
{
    public string DisplayText => $"{Code} - {Name}";

    public override string ToString() => DisplayText;
}

public partial class IncomingSeedVehicleConfigViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<IncomingSeedVehicleListItem> _allSeedVehicles = [];

    [ObservableProperty] private string _searchCustomerKeyword = string.Empty;
    [ObservableProperty] private string _searchProductKeyword = string.Empty;
    [ObservableProperty] private ObservableCollection<IncomingSeedVehicleListItem> _seedVehicles = new();
    [ObservableProperty] private ObservableCollection<IncomingSeedVehicleMasterOption> _customerOptions = new();
    [ObservableProperty] private ObservableCollection<IncomingSeedVehicleMasterOption> _productOptions = new();
    [ObservableProperty] private IncomingSeedVehicleListItem? _selectedSeedVehicle;
    [ObservableProperty] private IncomingSeedVehicleMasterOption? _selectedCustomerOption;
    [ObservableProperty] private IncomingSeedVehicleMasterOption? _selectedProductOption;
    [ObservableProperty] private int _sortOrder = 10;
    [ObservableProperty] private bool _editIsActive = true;

    public IncomingSeedVehicleConfigViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    partial void OnSelectedSeedVehicleChanged(IncomingSeedVehicleListItem? value)
    {
        if (value == null)
        {
            return;
        }

        SelectedCustomerOption = CustomerOptions.FirstOrDefault(x => string.Equals(x.Code, value.CustomerCode, StringComparison.OrdinalIgnoreCase));
        SelectedProductOption = ProductOptions.FirstOrDefault(x => string.Equals(x.Code, value.ProductCode, StringComparison.OrdinalIgnoreCase));
        SortOrder = value.SortOrder;
        EditIsActive = value.IsActive;
    }

    public async Task LoadAsync()
    {
        await ReloadOptionsAsync();
        await ReloadSeedVehiclesAsync();
        SortOrder = GetNextLocalSortOrder();
    }

    [RelayCommand]
    private void Search()
    {
        ApplySeedVehicleFilter();
    }

    [RelayCommand]
    private void ResetForm()
    {
        SelectedSeedVehicle = null;
        SelectedCustomerOption = null;
        SelectedProductOption = null;
        SortOrder = GetNextLocalSortOrder();
        EditIsActive = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialog = scope.ServiceProvider.GetRequiredService<IDialogService>();

        if (SelectedCustomerOption == null || string.IsNullOrWhiteSpace(SelectedCustomerOption.Code))
        {
            await dialog.ShowWarningAsync(
                "Thi\u1ebfu th\u00f4ng tin",
                "Vui l\u00f2ng ch\u1ecdn Kh\u00e1ch h\u00e0ng.");
            return;
        }

        if (SelectedProductOption == null || string.IsNullOrWhiteSpace(SelectedProductOption.Code))
        {
            await dialog.ShowWarningAsync(
                "Thi\u1ebfu th\u00f4ng tin",
                "Vui l\u00f2ng ch\u1ecdn S\u1ea3n ph\u1ea9m.");
            return;
        }

        try
        {
            if (SelectedSeedVehicle == null)
            {
                var create = scope.ServiceProvider.GetRequiredService<CreateIncomingSeedVehicleUseCase>();
                var result = await create.ExecuteAsync(
                    new CreateIncomingSeedVehicleRequest(SelectedCustomerOption.Code, SelectedProductOption.Code, null, EditIsActive),
                    CancellationToken.None);

                if (!result.Success)
                {
                    await dialog.ShowWarningAsync(
                        "Kh\u00f4ng th\u1ec3 l\u01b0u",
                        result.ErrorMessage ?? "Kh\u00f4ng th\u1ec3 t\u1ea1o xe nh\u1eadp m\u1eabu.");
                    return;
                }
            }
            else
            {
                var update = scope.ServiceProvider.GetRequiredService<UpdateIncomingSeedVehicleUseCase>();
                var result = await update.ExecuteAsync(
                    new UpdateIncomingSeedVehicleRequest(SelectedSeedVehicle.Id, SelectedCustomerOption.Code, SelectedProductOption.Code, SelectedSeedVehicle.SortOrder, EditIsActive),
                    CancellationToken.None);

                if (!result.Success)
                {
                    await dialog.ShowWarningAsync(
                        "Kh\u00f4ng th\u1ec3 l\u01b0u",
                        result.ErrorMessage ?? "Kh\u00f4ng th\u1ec3 c\u1eadp nh\u1eadt xe nh\u1eadp m\u1eabu.");
                    return;
                }
            }

            await dialog.ShowInfoAsync(
                "Th\u00f4ng b\u00e1o",
                "L\u01b0u xe nh\u1eadp m\u1eabu th\u00e0nh c\u00f4ng.");
            ResetForm();
            await ReloadSeedVehiclesAsync();
        }
        catch (Exception ex)
        {
            await dialog.ShowErrorAsync(
                "L\u1ed7i h\u1ec7 th\u1ed1ng",
                $"L\u1ed7i khi l\u01b0u xe nh\u1eadp m\u1eabu: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSeedVehicle == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialog = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var confirmed = await dialog.ShowConfirmAsync(
            "X\u00e1c nh\u1eadn",
            $"B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n x\u00f3a xe nh\u1eadp m\u1eabu {SelectedSeedVehicle.CustomerName} - {SelectedSeedVehicle.ProductName}?",
            "X\u00f3a",
            "H\u1ee7y");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var delete = scope.ServiceProvider.GetRequiredService<DeleteIncomingSeedVehicleUseCase>();
            var result = await delete.ExecuteAsync(SelectedSeedVehicle.Id, CancellationToken.None);
            if (!result.Success)
            {
                await dialog.ShowWarningAsync(
                    "Kh\u00f4ng th\u1ec3 x\u00f3a",
                    result.ErrorMessage ?? "Kh\u00f4ng th\u1ec3 x\u00f3a xe nh\u1eadp m\u1eabu.");
                return;
            }

            await dialog.ShowInfoAsync(
                "Th\u00f4ng b\u00e1o",
                "\u0110\u00e3 x\u00f3a xe nh\u1eadp m\u1eabu.");
            ResetForm();
            await ReloadSeedVehiclesAsync();
        }
        catch (Exception ex)
        {
            await dialog.ShowErrorAsync(
                "L\u1ed7i h\u1ec7 th\u1ed1ng",
                $"L\u1ed7i khi x\u00f3a xe nh\u1eadp m\u1eabu: {ex.Message}");
        }
    }

    private async Task ReloadSeedVehiclesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var get = scope.ServiceProvider.GetRequiredService<GetIncomingSeedVehiclesUseCase>();
        var list = await get.ExecuteAsync(CancellationToken.None);

        _allSeedVehicles.Clear();
        _allSeedVehicles.AddRange(list);
        ApplySeedVehicleFilter();
    }

    private void ApplySeedVehicleFilter()
    {
        var filtered = _allSeedVehicles.Where(x =>
            MatchesSearch(x.CustomerCode, SearchCustomerKeyword)
            || MatchesSearch(x.CustomerName, SearchCustomerKeyword));

        filtered = filtered.Where(x =>
            MatchesSearch(x.ProductCode, SearchProductKeyword)
            || MatchesSearch(x.ProductName, SearchProductKeyword));

        SeedVehicles.Clear();
        foreach (var item in filtered)
        {
            SeedVehicles.Add(item);
        }
    }

    private static bool MatchesSearch(string? source, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task ReloadOptionsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var customers = await scope.ServiceProvider.GetRequiredService<ICustomerRepository>().SearchAsync(null, CancellationToken.None);
        var products = await scope.ServiceProvider.GetRequiredService<IProductRepository>().SearchAsync(null, CancellationToken.None);

        CustomerOptions.Clear();
        foreach (var customer in customers
            .Where(x => x.IsActive && CustomerBusinessRoles.AllowsTransaction(x.CustomerBusinessRole, TransactionType.INBOUND))
            .OrderBy(x => x.CustomerName))
        {
            CustomerOptions.Add(new IncomingSeedVehicleMasterOption(customer.CustomerCode, customer.CustomerName));
        }

        ProductOptions.Clear();
        foreach (var product in products
            .Where(x => x.IsActive
                && ProductTransactionScopes.AllowsTransaction(x.TransactionScope, TransactionType.INBOUND)
                && string.Equals(ProductTypes.Normalize(x.ProductType), ProductTypes.Inbound, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ProductName))
        {
            ProductOptions.Add(new IncomingSeedVehicleMasterOption(product.ProductCode, product.ProductName));
        }
    }

    private int GetNextLocalSortOrder()
        => _allSeedVehicles.Count == 0 ? 10 : _allSeedVehicles.Max(x => x.SortOrder) + 10;
}
