using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public sealed record CustomerSearchRoleOption(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public partial class CustomerMasterViewModel : ObservableObject
{
    private const string SearchRoleAll = "ALL";

    private readonly IServiceScopeFactory _scopeFactory;

    public CustomerMasterViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        SelectedSearchCustomerRoleOption = SearchCustomerRoleOptions.FirstOrDefault();
    }

    [ObservableProperty] private string _searchCode = string.Empty;
    [ObservableProperty] private string _searchName = string.Empty;
    [ObservableProperty] private CustomerSearchRoleOption? _selectedSearchCustomerRoleOption;

    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private bool _editIsSupplier;
    [ObservableProperty] private bool _editIsDistributor;
    [ObservableProperty] private bool _editIsActive = true;

    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private Customer? _selectedCustomer;

    public IReadOnlyList<CustomerSearchRoleOption> SearchCustomerRoleOptions { get; } =
    [
        new(SearchRoleAll, "T\u1ea5t c\u1ea3"),
        new(CustomerBusinessRoles.Supplier, "NCC"),
        new(CustomerBusinessRoles.Distributor, "NPP")
    ];

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        if (value == null)
        {
            return;
        }

        EditCode = value.CustomerCode;
        EditName = value.CustomerName;
        EditIsSupplier = CustomerBusinessRoles.AllowsTransaction(value.CustomerBusinessRole, TransactionType.INBOUND);
        EditIsDistributor = CustomerBusinessRoles.AllowsTransaction(value.CustomerBusinessRole, TransactionType.OUTBOUND);
        EditIsActive = value.IsActive;
    }

    public async Task LoadAsync()
    {
        SelectedSearchCustomerRoleOption ??= SearchCustomerRoleOptions.FirstOrDefault();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        var list = await repo.SearchAsync(null, CancellationToken.None);
        var filtered = list.Where(x =>
            MatchesSearch(x.CustomerCode, SearchCode)
            && MatchesSearch(x.CustomerName, SearchName)
            && MatchesSearchRole(x));

        Customers.Clear();
        foreach (var item in filtered)
        {
            Customers.Add(item);
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

    private bool MatchesSearchRole(Customer customer)
    {
        return (SelectedSearchCustomerRoleOption?.Value ?? SearchRoleAll) switch
        {
            CustomerBusinessRoles.Supplier => CustomerBusinessRoles.AllowsTransaction(customer.CustomerBusinessRole, TransactionType.INBOUND),
            CustomerBusinessRoles.Distributor => CustomerBusinessRoles.AllowsTransaction(customer.CustomerBusinessRole, TransactionType.OUTBOUND),
            _ => true
        };
    }

    [RelayCommand]
    private void ResetForm()
    {
        EditCode = string.Empty;
        EditName = string.Empty;
        EditIsSupplier = false;
        EditIsDistributor = false;
        EditIsActive = true;
        SelectedCustomer = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
        {
            await dialogService.ShowErrorAsync(
                "L\u1ed7i",
                "M\u00e3 v\u00e0 T\u00ean kh\u00e1ch h\u00e0ng kh\u00f4ng \u0111\u01b0\u1ee3c r\u1ed7ng!");
            return;
        }

        var normalizedRole = ResolveCustomerBusinessRole();
        if (normalizedRole == null)
        {
            await dialogService.ShowWarningAsync(
                "Thi\u1ebfu th\u00f4ng tin",
                "Vui l\u00f2ng t\u00edch NCC ho\u1eb7c NPP.");
            return;
        }

        try
        {
            var code = EditCode.Trim();
            var name = EditName.Trim();
            var existing = await repo.GetByCodeAsync(code, CancellationToken.None);
            if (existing != null && (SelectedCustomer == null || existing.Id != SelectedCustomer.Id))
            {
                await dialogService.ShowWarningAsync(
                    "L\u1ed7i",
                    "M\u00e3 kh\u00e1ch h\u00e0ng \u0111\u00e3 t\u1ed3n t\u1ea1i tr\u00ean h\u1ec7 th\u1ed1ng!");
                return;
            }

            if (SelectedCustomer == null)
            {
                var newCustomer = new Customer
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = code,
                    CustomerName = name,
                    CustomerBusinessRole = normalizedRole,
                    IsActive = EditIsActive,
                    CreatedAt = clock.NowLocal,
                    CreatedBy = "Operator"
                };
                await repo.AddAsync(newCustomer, CancellationToken.None);
                await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, newCustomer, clock.NowLocal);
            }
            else
            {
                var target = existing ?? SelectedCustomer;
                target.CustomerCode = code;
                target.CustomerName = name;
                target.CustomerBusinessRole = normalizedRole;
                target.IsActive = EditIsActive;
                target.UpdatedAt = clock.NowLocal;
                target.UpdatedBy = "Operator";

                await repo.UpdateAsync(target, CancellationToken.None);
                await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, target, clock.NowLocal);
            }

            await uow.SaveChangesAsync(CancellationToken.None);
            await dialogService.ShowInfoAsync(
                "Th\u00f4ng b\u00e1o",
                "L\u01b0u d\u1eef li\u1ec7u th\u00e0nh c\u00f4ng!");

            ResetForm();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync(
                "L\u1ed7i h\u1ec7 th\u1ed1ng",
                $"L\u1ed7i khi l\u01b0u d\u1eef li\u1ec7u: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (SelectedCustomer == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        var result = await dialogService.ShowConfirmAsync(
            "X\u00e1c nh\u1eadn",
            $"B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n ng\u1eebng s\u1eed d\u1ee5ng kh\u00e1ch h\u00e0ng {SelectedCustomer.CustomerName}?",
            "\u0110\u1ed3ng \u00fd",
            "B\u1ecf qua");

        if (!result)
        {
            return;
        }

        var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

        SelectedCustomer.IsActive = false;
        SelectedCustomer.UpdatedAt = clock.NowLocal;
        SelectedCustomer.UpdatedBy = "Operator";
        await repo.UpdateAsync(SelectedCustomer, CancellationToken.None);
        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedCustomer, clock.NowLocal);
        await uow.SaveChangesAsync(CancellationToken.None);

        await dialogService.ShowInfoAsync(
            "Th\u00f4ng b\u00e1o",
            "\u0110\u00e3 chuy\u1ec3n \u0111\u1ed5i tr\u1ea1ng th\u00e1i ng\u1eebng s\u1eed d\u1ee5ng.");
        ResetForm();
        await SearchAsync();
    }

    private string? ResolveCustomerBusinessRole()
        => (EditIsSupplier, EditIsDistributor) switch
        {
            (true, true) => CustomerBusinessRoles.Both,
            (true, false) => CustomerBusinessRoles.Supplier,
            (false, true) => CustomerBusinessRoles.Distributor,
            _ => null
        };

    private static async Task EnqueueMasterSyncAsync(
        ISyncOutboxRepository outboxRepo,
        ISyncPayloadFactory payloadFactory,
        Customer customer,
        DateTime now)
    {
        await outboxRepo.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = customer.Id,
            AggregateType = SyncAggregateTypes.Customer,
            PayloadJson = payloadFactory.CreatePayload(customer),
            IdempotencyKey = customer.Id,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, CancellationToken.None);
    }
}
