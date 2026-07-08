using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;

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
    [ObservableProperty] private string _selectedExportPackageType = ExportPackageTypes.Bagged;
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

    public IReadOnlyList<ExportPackageTypeOption> ExportPackageTypeOptions { get; } =
    [
        new(ExportPackageTypes.Bagged, "\u0110\u00f3ng bao"),
        new(ExportPackageTypes.Bulk, "R\u1eddi")
    ];

    public bool IsBaggedExportPackage => SelectedExportPackageType == ExportPackageTypes.Bagged;
    public bool IsBagMetricsEnabled => IsBaggedExportPackage;

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

    public CreateTemporaryExportCutOrderDialogViewModel(
        IServiceScopeFactory scopeFactory,
        ExportScaleCutOrderListItem cutOrder)
        : this(scopeFactory)
    {
        Title = "S\u1eeda c\u1eaft l\u1ec7nh t\u1ea1m";
        SetCustomerCode(cutOrder.CustomerCode);
        SetCustomerName(cutOrder.CustomerName);
        SetProductCode(cutOrder.ProductCode);
        SetProductName(cutOrder.ProductName);
        ProductType = cutOrder.ProductType ?? string.Empty;
        SelectedExportPackageType = ExportPackageTypes.ResolveForExistingData(cutOrder.ExportPackageType, cutOrder.BagWeightKg);
        PlannedWeightTonsInput = FormatNumber((cutOrder.PlannedWeight ?? 0m) / 1000m);
        TareWeightKgInput = FormatNumber(cutOrder.TareWeightKg ?? 0m);
        BagWeightKgInput = FormatNumber(cutOrder.BagWeightKg ?? 0m);
        Notes = cutOrder.Notes ?? string.Empty;
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

        var customerCode = NormalizeRequired(CustomerCode, "M\u00e3 kh\u00e1ch h\u00e0ng");
        var customerName = NormalizeRequired(CustomerName, "Kh\u00e1ch h\u00e0ng");
        var productCode = NormalizeRequired(ProductCode, "M\u00e3 s\u1ea3n ph\u1ea9m");
        var productName = NormalizeRequired(ProductName, "S\u1ea3n ph\u1ea9m");
        var exportPackageType = ExportPackageTypes.Normalize(SelectedExportPackageType);
        var plannedWeightTons = ParseRequiredDecimal(PlannedWeightTonsInput, "S\u1ed1 l\u01b0\u1ee3ng \u0111\u1eb7t (t\u1ea5n)", mustBePositive: true);
        decimal? tareWeightKg = 0m;
        decimal? bagWeightKg = 0m;

        if (string.IsNullOrWhiteSpace(exportPackageType))
        {
            ValidationMessage = "Lo\u1ea1i l\u00e0 b\u1eaft bu\u1ed9c.";
            return;
        }

        if (exportPackageType == ExportPackageTypes.Bagged)
        {
            tareWeightKg = ParseRequiredDecimal(TareWeightKgInput, "TL v\u1ecf (kg)", mustBePositive: false);
            bagWeightKg = ParseRequiredDecimal(BagWeightKgInput, "TL bao (kg)", mustBePositive: true);
        }

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
            exportPackageType,
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

    partial void OnSelectedExportPackageTypeChanged(string value)
    {
        ClearValidation();
        if (value == ExportPackageTypes.Bulk)
        {
            TareWeightKgInput = "0";
            BagWeightKgInput = "0";
        }

        OnPropertyChanged(nameof(IsBaggedExportPackage));
        OnPropertyChanged(nameof(IsBagMetricsEnabled));
        RecalculatePreview();
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

        if (!IsBaggedExportPackage)
        {
            BagCountPreview = "0";
            return;
        }

        if (!TryParseDecimal(PlannedWeightTonsInput, out var plannedWeightTons)
            || plannedWeightTons <= 0m)
        {
            return;
        }

        if (!TryParseDecimal(BagWeightKgInput, out var bagWeightKg) || bagWeightKg <= 0m)
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
            FractionalBagWarningMessage = "S\u1ed1 l\u01b0\u1ee3ng \u0111\u1eb7t chia cho TL bao \u0111ang ra s\u1ed1 l\u1ebb, h\u1ec7 th\u1ed1ng s\u1ebd l\u00e0m tr\u00f2n s\u1ed1 bao theo quy t\u1eafc chu\u1ea9n.";
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

    private static string FormatNumber(decimal value)
        => value == 0m
            ? "0"
            : value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ExportPackageTypeOption(string Value, string DisplayName);

public sealed record CreateTemporaryExportCutOrderDialogResult(
    string? CustomerCode,
    string CustomerName,
    string? ProductCode,
    string ProductName,
    string? ProductType,
    string ExportPackageType,
    decimal PlannedWeightKg,
    decimal TareWeightKg,
    decimal BagWeightKg,
    string? Notes);
