using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases.MasterData;

public class AutocompleteService : IAutocompleteService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IVehicleRegistrationRepository _vehicleRegistrationRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public AutocompleteService(
        IVehicleRepository vehicleRepository,
        IVehicleRegistrationRepository vehicleRegistrationRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository)
    {
        _vehicleRepository = vehicleRepository;
        _vehicleRegistrationRepository = vehicleRegistrationRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<AutocompleteItem>> SearchAsync(AutocompleteQuery query, CancellationToken ct)
    {
        var keyword = Normalize(query.SearchText);
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<AutocompleteItem>();
        }

        var limit = Math.Clamp(query.Limit, 1, 20);

        return query.FieldType switch
        {
            AutocompleteFieldType.Vehicle => await SearchVehicleAsync(keyword, limit, ct),
            AutocompleteFieldType.Mooc => await SearchMoocAsync(keyword, limit, ct),
            AutocompleteFieldType.Driver => await SearchDriverAsync(keyword, limit, ct),
            AutocompleteFieldType.Customer => await SearchCustomerAsync(keyword, limit, ct),
            AutocompleteFieldType.ProductCode => await SearchProductCodeAsync(keyword, limit, ct),
            AutocompleteFieldType.ProductName => await SearchProductNameAsync(keyword, limit, ct),
            _ => Array.Empty<AutocompleteItem>()
        };
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchVehicleAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _vehicleRepository.SearchVehicleSourcesAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchVehicleHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .GroupBy(x => Normalize(x.VehiclePlate))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.VehiclePlate, keyword))
            .ThenBy(x => x.VehiclePlate, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.VehiclePlate,
                x.VehiclePlate,
                BuildVehicleSecondaryText(x.MoocNumber, x.DriverName),
                AutocompleteFieldType.Vehicle,
                new AutocompletePayload(
                    VehiclePlate: x.VehiclePlate,
                    MoocNumber: x.MoocNumber,
                    DriverName: x.DriverName,
                    TtcpWeight: x.TtcpWeight,
                    VehicleRegistrationNo: x.VehicleRegistrationNo,
                    VehicleRegistrationExpiryDate: x.VehicleRegistrationExpiryDate,
                    MoocRegistrationNo: x.MoocRegistrationNo,
                    MoocRegistrationExpiryDate: x.MoocRegistrationExpiryDate)))
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchMoocAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _vehicleRepository.SearchMoocSourcesAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchMoocHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .Where(x => !string.IsNullOrWhiteSpace(x.MoocNumber))
            .GroupBy(x => Normalize(x.MoocNumber))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.MoocNumber, keyword))
            .ThenBy(x => x.MoocNumber, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.MoocNumber!,
                x.MoocNumber!,
                string.IsNullOrWhiteSpace(x.VehiclePlate) ? null : $"PTVC {x.VehiclePlate}",
                AutocompleteFieldType.Mooc,
                new AutocompletePayload(
                    VehiclePlate: x.VehiclePlate,
                    MoocNumber: x.MoocNumber,
                    DriverName: x.DriverName,
                    TtcpWeight: x.TtcpWeight,
                    VehicleRegistrationNo: x.VehicleRegistrationNo,
                    VehicleRegistrationExpiryDate: x.VehicleRegistrationExpiryDate,
                    MoocRegistrationNo: x.MoocRegistrationNo,
                    MoocRegistrationExpiryDate: x.MoocRegistrationExpiryDate)))
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchDriverAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _vehicleRepository.SearchDriverSourcesAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchDriverHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .GroupBy(x => Normalize(x.DriverName))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.DriverName, keyword))
            .ThenBy(x => x.DriverName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.DriverName,
                x.DriverName,
                BuildVehicleSecondaryText(x.VehiclePlate, x.MoocNumber, " | "),
                AutocompleteFieldType.Driver,
                new AutocompletePayload(
                    VehiclePlate: x.VehiclePlate,
                    MoocNumber: x.MoocNumber,
                    DriverName: x.DriverName)))
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchCustomerAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _customerRepository.SearchAutocompleteAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchCustomerHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .GroupBy(x => Normalize(x.CustomerCode) + "|" + Normalize(x.CustomerName))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.CustomerName, keyword))
            .ThenBy(x => x.CustomerName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.CustomerName,
                x.CustomerName,
                string.IsNullOrWhiteSpace(x.CustomerCode) ? null : $"Ma {x.CustomerCode}",
                AutocompleteFieldType.Customer,
                new AutocompletePayload(
                    CustomerCode: x.CustomerCode,
                    CustomerName: x.CustomerName)))
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchProductCodeAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _productRepository.SearchAutocompleteAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchProductCodeHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .GroupBy(x => Normalize(x.ProductCode))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.ProductCode, keyword))
            .ThenBy(x => x.ProductCode, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.ProductCode,
                x.ProductCode,
                x.ProductName,
                AutocompleteFieldType.ProductCode,
                new AutocompletePayload(
                    ProductCode: x.ProductCode,
                    ProductName: x.ProductName)))
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchProductNameAsync(string keyword, int limit, CancellationToken ct)
    {
        var master = await _productRepository.SearchAutocompleteAsync(keyword, limit, ct);
        var recent = await _vehicleRegistrationRepository.SearchProductNameHistorySourcesAsync(keyword, limit, ct);

        return master
            .Concat(recent)
            .GroupBy(x => Normalize(x.ProductCode) + "|" + Normalize(x.ProductName))
            .Select(x => x.First())
            .OrderByDescending(x => x.Source == "MASTER")
            .ThenBy(x => RankStartsWith(x.ProductName, keyword))
            .ThenBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new AutocompleteItem(
                x.ProductName,
                x.ProductName,
                x.ProductCode,
                AutocompleteFieldType.ProductName,
                new AutocompletePayload(
                    ProductCode: x.ProductCode,
                    ProductName: x.ProductName)))
            .ToList()
            .AsReadOnly();
    }

    private static string Normalize(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static int RankStartsWith(string? value, string keyword)
        => Normalize(value).StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static string? BuildVehicleSecondaryText(string? first, string? second, string separator = " | ")
    {
        var parts = new[] { first, second }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(separator, parts);
    }
}
