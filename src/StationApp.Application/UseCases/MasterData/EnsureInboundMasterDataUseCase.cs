using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases.MasterData;

public sealed class EnsureInboundMasterDataUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUserContext;

    public EnsureInboundMasterDataUseCase(
        IVehicleRepository vehicleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IClock clock,
        ICurrentUserContext currentUserContext)
    {
        _vehicleRepository = vehicleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _clock = clock;
        _currentUserContext = currentUserContext;
    }

    public async Task ExecuteAsync(
        string vehiclePlate,
        string? moocNumber,
        string? driverName,
        TransportMethod? transportMethod,
        string? customerCode,
        string? customerName,
        string? productCode,
        string? productName,
        CancellationToken ct,
        decimal? ttcpWeight = null,
        string? vehicleRegistrationNo = null,
        DateTime? vehicleRegistrationExpiryDate = null,
        string? moocRegistrationNo = null,
        DateTime? moocRegistrationExpiryDate = null)
    {
        await EnsureVehicleAsync(vehiclePlate, moocNumber, driverName, transportMethod,
            ttcpWeight, vehicleRegistrationNo, vehicleRegistrationExpiryDate,
            moocRegistrationNo, moocRegistrationExpiryDate, ct);
        await EnsureCustomerAsync(customerCode, customerName, ct);
        await EnsureProductAsync(productCode, productName, ct);
    }

    private async Task EnsureVehicleAsync(
        string vehiclePlate,
        string? moocNumber,
        string? driverName,
        TransportMethod? transportMethod,
        decimal? ttcpWeight,
        string? vehicleRegistrationNo,
        DateTime? vehicleRegistrationExpiryDate,
        string? moocRegistrationNo,
        DateTime? moocRegistrationExpiryDate,
        CancellationToken ct)
    {
        var normalizedPlate = vehiclePlate.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return;
        }

        var normalizedMooc = NormalizeOptional(moocNumber);
        var normalizedDriver = NormalizeOptional(driverName);
        var normalizedTransportMethod = transportMethod?.ToString();
        var normalizedVehicleRegNo = NormalizeOptional(vehicleRegistrationNo);
        var normalizedMoocRegNo = NormalizeOptional(moocRegistrationNo);

        Vehicle? existing = null;
        var byPlate = await _vehicleRepository.GetByPlateAsync(normalizedPlate, ct);

        if (!string.IsNullOrWhiteSpace(normalizedMooc))
        {
            existing = byPlate.FirstOrDefault(x => string.Equals(x.MoocNumber, normalizedMooc, StringComparison.OrdinalIgnoreCase))
                ?? byPlate.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.MoocNumber));
        }
        else
        {
            existing = byPlate.FirstOrDefault();
        }

        if (existing == null)
        {
            await _vehicleRepository.AddAsync(new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = normalizedPlate,
                MoocNumber = normalizedMooc ?? string.Empty,
                DriverName = normalizedDriver,
                TransportMethod = normalizedTransportMethod,
                TtcpWeight = ttcpWeight,
                VehicleRegistrationNo = normalizedVehicleRegNo,
                VehicleRegistrationExpiryDate = vehicleRegistrationExpiryDate,
                MoocRegistrationNo = normalizedMoocRegNo,
                MoocRegistrationExpiryDate = moocRegistrationExpiryDate,
                IsActive = true,
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUserContext.Username
            }, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.MoocNumber) && !string.IsNullOrWhiteSpace(normalizedMooc))
        {
            existing.MoocNumber = normalizedMooc;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.DriverName) && !string.IsNullOrWhiteSpace(normalizedDriver))
        {
            existing.DriverName = normalizedDriver;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.TransportMethod) && !string.IsNullOrWhiteSpace(normalizedTransportMethod))
        {
            existing.TransportMethod = normalizedTransportMethod;
            changed = true;
        }

        if (existing.TtcpWeight == null && ttcpWeight != null)
        {
            existing.TtcpWeight = ttcpWeight;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.VehicleRegistrationNo) && !string.IsNullOrWhiteSpace(normalizedVehicleRegNo))
        {
            existing.VehicleRegistrationNo = normalizedVehicleRegNo;
            changed = true;
        }

        if (existing.VehicleRegistrationExpiryDate == null && vehicleRegistrationExpiryDate != null)
        {
            existing.VehicleRegistrationExpiryDate = vehicleRegistrationExpiryDate;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.MoocRegistrationNo) && !string.IsNullOrWhiteSpace(normalizedMoocRegNo))
        {
            existing.MoocRegistrationNo = normalizedMoocRegNo;
            changed = true;
        }

        if (existing.MoocRegistrationExpiryDate == null && moocRegistrationExpiryDate != null)
        {
            existing.MoocRegistrationExpiryDate = moocRegistrationExpiryDate;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = _clock.NowLocal;
        existing.UpdatedBy = _currentUserContext.Username;
        await _vehicleRepository.UpdateAsync(existing, ct);
    }

    private async Task EnsureCustomerAsync(string? customerCode, string? customerName, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(customerCode);
        var normalizedName = NormalizeOptional(customerName);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var existing = await _customerRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            await _customerRepository.AddAsync(new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = normalizedCode,
                CustomerName = normalizedName,
                IsActive = true,
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUserContext.Username
            }, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.CustomerName) && !string.IsNullOrWhiteSpace(normalizedName))
        {
            existing.CustomerName = normalizedName;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = _clock.NowLocal;
        existing.UpdatedBy = _currentUserContext.Username;
        await _customerRepository.UpdateAsync(existing, ct);
    }

    private async Task EnsureProductAsync(string? productCode, string? productName, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(productCode);
        var normalizedName = NormalizeOptional(productName);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var existing = await _productRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            await _productRepository.AddAsync(new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = normalizedCode,
                ProductName = normalizedName,
                IsActive = true,
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUserContext.Username
            }, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.ProductName) && !string.IsNullOrWhiteSpace(normalizedName))
        {
            existing.ProductName = normalizedName;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = _clock.NowLocal;
        existing.UpdatedBy = _currentUserContext.Username;
        await _productRepository.UpdateAsync(existing, ct);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
