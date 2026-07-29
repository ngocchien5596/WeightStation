using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class CreateClayVesselDialogViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _isSynchronizingLinkedFields;

    [ObservableProperty] private string _title = "Tạo tàu";
    [ObservableProperty] private string _vesselName = string.Empty;
    [ObservableProperty] private string _customerCode = string.Empty;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _validationMessage = string.Empty;

    public AutocompleteInputViewModel CustomerCodeInput { get; }
    public AutocompleteInputViewModel CustomerNameInput { get; }
    public AutocompleteInputViewModel ProductCodeInput { get; }
    public AutocompleteInputViewModel ProductNameInput { get; }

    public CreateClayVesselDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public CreateClayVesselDialogViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        CustomerCodeInput = CreateAutocompleteField(AutocompleteFieldType.CustomerCode, 1, ApplyCustomerSelection);
        CustomerNameInput = CreateAutocompleteField(AutocompleteFieldType.Customer, 2, ApplyCustomerSelection);
        ProductCodeInput = CreateAutocompleteField(AutocompleteFieldType.ProductCode, 1, ApplyProductSelection);
        ProductNameInput = CreateAutocompleteField(AutocompleteFieldType.ProductName, 2, ApplyProductSelection);

        WireTextState(CustomerCodeInput, value => CustomerCode = value ?? string.Empty);
        WireTextState(CustomerNameInput, value => CustomerName = value ?? string.Empty);
        WireTextState(ProductCodeInput, value => ProductCode = value ?? string.Empty);
        WireTextState(ProductNameInput, value => ProductName = value ?? string.Empty);
    }

    public CreateClayVesselDialogViewModel(IServiceScopeFactory scopeFactory, ClayVesselListItem vessel)
        : this(scopeFactory)
    {
        Title = "Sửa tàu";
        VesselName = vessel.VesselName;
        SetCustomerCode(vessel.CustomerCode);
        SetCustomerName(vessel.CustomerName);
        SetProductCode(vessel.ProductCode);
        SetProductName(vessel.ProductName);
        Notes = vessel.Notes ?? string.Empty;
    }

    private AutocompleteInputViewModel CreateAutocompleteField(
        AutocompleteFieldType fieldType,
        int minimumPrefixLength,
        Action<AutocompleteItem> onSelected)
    {
        return new AutocompleteInputViewModel(
            (keyword, ct) => SearchAutocompleteAsync(fieldType, keyword, ct),
            onSelected,
            minimumPrefixLength);
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchAutocompleteAsync(
        AutocompleteFieldType fieldType,
        string keyword,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutocompleteService>();
        return await service.SearchAsync(new AutocompleteQuery(fieldType, keyword, TransactionType: TransactionType.INBOUND), ct);
    }

    private static void WireTextState(AutocompleteInputViewModel state, Action<string?> setter)
    {
        state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AutocompleteInputViewModel.Text))
            {
                setter(state.Text);
            }
        };
    }

    private void ApplyCustomerSelection(AutocompleteItem item)
    {
        SetCustomerCode(item.Payload?.CustomerCode ?? item.Value);
        SetCustomerName(item.Payload?.CustomerName ?? item.Value);
    }

    private void ApplyProductSelection(AutocompleteItem item)
    {
        SetProductCode(item.Payload?.ProductCode ?? item.Value);
        SetProductName(item.Payload?.ProductName ?? item.Value);
    }

    [RelayCommand]
    private void Confirm()
    {
        ValidationMessage = string.Empty;

        var vesselName = NormalizeRequired(VesselName, "Tên phương tiện");
        if (vesselName == null)
        {
            return;
        }

        var customerCode = NormalizeRequired(CustomerCode, "Mã đơn vị vận chuyển");
        if (customerCode == null)
        {
            return;
        }

        var customerName = NormalizeRequired(CustomerName, "Đơn vị vận chuyển");
        if (customerName == null)
        {
            return;
        }

        var productCode = NormalizeRequired(ProductCode, "Mã hàng");
        if (productCode == null)
        {
            return;
        }

        var productName = NormalizeRequired(ProductName, "Hàng hóa");
        if (productName == null)
        {
            return;
        }

        DialogResultValue = new CreateClayVesselDialogResult(
            vesselName,
            customerCode,
            customerName,
            productCode,
            productName,
            NormalizeOptional(Notes));
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    partial void OnCustomerCodeChanged(string value)
    {
        ClearValidation();
        if (!_isSynchronizingLinkedFields && string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(CustomerName))
        {
            SetCustomerName(null);
        }
    }

    partial void OnCustomerNameChanged(string value)
    {
        ClearValidation();
        if (!_isSynchronizingLinkedFields && string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(CustomerCode))
        {
            SetCustomerCode(null);
        }
    }

    partial void OnProductCodeChanged(string value)
    {
        ClearValidation();
        if (!_isSynchronizingLinkedFields && string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(ProductName))
        {
            SetProductName(null);
        }
    }

    partial void OnProductNameChanged(string value)
    {
        ClearValidation();
        if (!_isSynchronizingLinkedFields && string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(ProductCode))
        {
            SetProductCode(null);
        }
    }

    private void SetCustomerCode(string? value)
    {
        _isSynchronizingLinkedFields = true;
        CustomerCode = value ?? string.Empty;
        CustomerCodeInput.SetText(CustomerCode);
        _isSynchronizingLinkedFields = false;
    }

    private void SetCustomerName(string? value)
    {
        _isSynchronizingLinkedFields = true;
        CustomerName = value ?? string.Empty;
        CustomerNameInput.SetText(CustomerName);
        _isSynchronizingLinkedFields = false;
    }

    private void SetProductCode(string? value)
    {
        _isSynchronizingLinkedFields = true;
        ProductCode = value ?? string.Empty;
        ProductCodeInput.SetText(ProductCode);
        _isSynchronizingLinkedFields = false;
    }

    private void SetProductName(string? value)
    {
        _isSynchronizingLinkedFields = true;
        ProductName = value ?? string.Empty;
        ProductNameInput.SetText(ProductName);
        _isSynchronizingLinkedFields = false;
    }

    private void ClearValidation()
    {
        if (!string.IsNullOrEmpty(ValidationMessage))
        {
            ValidationMessage = string.Empty;
        }
    }

    private string? NormalizeRequired(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            ValidationMessage = $"{fieldName} là bắt buộc.";
            return null;
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreateClayVesselDialogResult(
    string VesselName,
    string CustomerCode,
    string CustomerName,
    string ProductCode,
    string ProductName,
    string? Notes
);
