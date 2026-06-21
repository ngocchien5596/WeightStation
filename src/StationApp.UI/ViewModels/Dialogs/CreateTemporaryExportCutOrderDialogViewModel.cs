using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class CreateTemporaryExportCutOrderDialogViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private bool _isSynchronizingLinkedFields;

    [ObservableProperty] private string _title = "T\u1ea1o c\u1eaft l\u1ec7nh t\u1ea1m";
    [ObservableProperty] private string _customerCode = string.Empty;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _productCode = string.Empty;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _productType = string.Empty;
    [ObservableProperty] private string _plannedWeightTonsInput = string.Empty;
    [ObservableProperty] private string _tareWeightKgInput = string.Empty;
    [ObservableProperty] private string _bagWeightKgInput = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _bagCountPreview = string.Empty;
    [ObservableProperty] private bool _hasFractionalBagWarning;
    [ObservableProperty] private string _fractionalBagWarningMessage = string.Empty;
    [ObservableProperty] private string _validationMessage = string.Empty;

    public AutocompleteInputViewModel CustomerCodeInput { get; }
    public AutocompleteInputViewModel CustomerNameInput { get; }
    public AutocompleteInputViewModel ProductCodeInput { get; }
    public AutocompleteInputViewModel ProductNameInput { get; }

    public CreateTemporaryExportCutOrderDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public CreateTemporaryExportCutOrderDialogViewModel(IServiceScopeFactory scopeFactory)
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

        RecalculatePreview();
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
        return await service.SearchAsync(new AutocompleteQuery(fieldType, keyword), ct);
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
        ProductType = item.Payload?.ProductType ?? string.Empty;
    }

    [RelayCommand]
    private void Confirm()
    {
        ValidationMessage = string.Empty;

        var customerCode = NormalizeRequired(CustomerCode, "Mã khách hàng");
        var customerName = NormalizeRequired(CustomerName, "Kh\u00e1ch h\u00e0ng");
        var productCode = NormalizeRequired(ProductCode, "Mã sản phẩm");
        var productName = NormalizeRequired(ProductName, "S\u1ea3n ph\u1ea9m");
        var plannedWeightTons = ParseRequiredDecimal(PlannedWeightTonsInput, "Số lượng đặt (tấn)", mustBePositive: true);
        var tareWeightKg = ParseRequiredDecimal(TareWeightKgInput, "Tr\u1ecdng l\u01b0\u1ee3ng v\u1ecf (kg)", mustBePositive: false);
        var bagWeightKg = ParseRequiredDecimal(BagWeightKgInput, "Tr\u1ecdng l\u01b0\u1ee3ng bao (kg)", mustBePositive: true);

        if (customerCode == null
            || customerName == null
            || productCode == null
            || productName == null
            || !plannedWeightTons.HasValue
            || !tareWeightKg.HasValue
            || !bagWeightKg.HasValue)
        {
            return;
        }

        DialogResultValue = new CreateTemporaryExportCutOrderDialogResult(
            customerCode,
            customerName,
            productCode,
            productName,
            string.IsNullOrWhiteSpace(ProductType) ? null : ProductType.Trim(),
            plannedWeightTons.Value * 1000m,
            tareWeightKg.Value,
            bagWeightKg.Value,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
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
        if (_isSynchronizingLinkedFields)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(CustomerName))
        {
            SetCustomerName(null);
        }
    }

    partial void OnCustomerNameChanged(string value)
    {
        ClearValidation();
        if (_isSynchronizingLinkedFields)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(CustomerCode))
        {
            SetCustomerCode(null);
        }
    }

    partial void OnProductCodeChanged(string value)
    {
        ClearValidation();
        if (_isSynchronizingLinkedFields)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            if (!string.IsNullOrWhiteSpace(ProductName))
            {
                SetProductName(null);
            }

            ProductType = string.Empty;
        }
    }

    partial void OnProductNameChanged(string value)
    {
        ClearValidation();
        if (_isSynchronizingLinkedFields)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            if (!string.IsNullOrWhiteSpace(ProductCode))
            {
                SetProductCode(null);
            }

            ProductType = string.Empty;
        }
    }

    partial void OnNotesChanged(string value) => ClearValidation();

    partial void OnPlannedWeightTonsInputChanged(string value)
    {
        ClearValidation();
        RecalculatePreview();
    }

    partial void OnTareWeightKgInputChanged(string value) => ClearValidation();

    partial void OnBagWeightKgInputChanged(string value)
    {
        ClearValidation();
        RecalculatePreview();
    }

    private void ClearValidation()
    {
        ValidationMessage = string.Empty;
    }

    private void SetCustomerCode(string? value)
    {
        UpdateLinkedField(() =>
        {
            CustomerCode = value?.Trim() ?? string.Empty;
            CustomerCodeInput.SetText(CustomerCode);
        });
    }

    private void SetCustomerName(string? value)
    {
        UpdateLinkedField(() =>
        {
            CustomerName = value?.Trim() ?? string.Empty;
            CustomerNameInput.SetText(CustomerName);
        });
    }

    private void SetProductCode(string? value)
    {
        UpdateLinkedField(() =>
        {
            ProductCode = value?.Trim() ?? string.Empty;
            ProductCodeInput.SetText(ProductCode);
        });
    }

    private void SetProductName(string? value)
    {
        UpdateLinkedField(() =>
        {
            ProductName = value?.Trim() ?? string.Empty;
            ProductNameInput.SetText(ProductName);
        });
    }

    private void UpdateLinkedField(Action action)
    {
        try
        {
            _isSynchronizingLinkedFields = true;
            action();
        }
        finally
        {
            _isSynchronizingLinkedFields = false;
        }
    }

    private void RecalculatePreview()
    {
        BagCountPreview = string.Empty;
        HasFractionalBagWarning = false;
        FractionalBagWarningMessage = string.Empty;

        if (!TryParseDecimal(PlannedWeightTonsInput, out var plannedWeightTons)
            || !TryParseDecimal(BagWeightKgInput, out var bagWeightKg)
            || plannedWeightTons <= 0m
            || bagWeightKg <= 0m)
        {
            return;
        }

        var plannedWeightKg = plannedWeightTons * 1000m;
        var exactBagCount = plannedWeightKg / bagWeightKg;
        var roundedBagCount = (int)decimal.Round(exactBagCount, 0, MidpointRounding.AwayFromZero);
        BagCountPreview = roundedBagCount.ToString("N0");

        if (plannedWeightKg % bagWeightKg != 0m)
        {
            HasFractionalBagWarning = true;
            FractionalBagWarningMessage = "Số lượng đặt chia cho trọng lượng bao đang ra số lẻ, hệ thống sẽ làm tròn số bao theo quy tắc chuẩn.";
        }
    }

    private string? NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ValidationMessage = $"{fieldName} l\u00e0 b\u1eaft bu\u1ed9c.";
            return null;
        }

        return value.Trim();
    }

    private decimal? ParseRequiredDecimal(string? value, string fieldName, bool mustBePositive)
    {
        if (!TryParseDecimal(value, out var parsed))
        {
            ValidationMessage = $"{fieldName} kh\u00f4ng h\u1ee3p l\u1ec7.";
            return null;
        }

        if (mustBePositive && parsed <= 0m)
        {
            ValidationMessage = $"{fieldName} ph\u1ea3i l\u1edbn h\u01a1n 0.";
            return null;
        }

        if (!mustBePositive && parsed < 0m)
        {
            ValidationMessage = $"{fieldName} ph\u1ea3i l\u1edbn h\u01a1n ho\u1eb7c b\u1eb1ng 0.";
            return null;
        }

        return decimal.Round(parsed, 3, MidpointRounding.AwayFromZero);
    }

    private static bool TryParseDecimal(string? value, out decimal parsed)
    {
        var normalized = value?.Trim().Replace(',', '.');
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed);
    }
}

public sealed record CreateTemporaryExportCutOrderDialogResult(
    string? CustomerCode,
    string CustomerName,
    string? ProductCode,
    string ProductName,
    string? ProductType,
    decimal PlannedWeightKg,
    decimal TareWeightKg,
    decimal BagWeightKg,
    string? Notes);
