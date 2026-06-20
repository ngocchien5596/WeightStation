using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases.MasterData;

public sealed class EnsureInboundMasterDataUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISyncOutboxRepository _syncOutboxRepository;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUserContext;

    public EnsureInboundMasterDataUseCase(
        IVehicleRepository vehicleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ISyncOutboxRepository syncOutboxRepository,
        ISyncPayloadFactory syncPayloadFactory,
        IClock clock,
        ICurrentUserContext currentUserContext)
    {
        _vehicleRepository = vehicleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _syncOutboxRepository = syncOutboxRepository;
        _syncPayloadFactory = syncPayloadFactory;
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
        string? productType,
        TransactionType transactionType,
        CancellationToken ct,
        decimal? ttcpWeight = null,
        string? vehicleRegistrationNo = null,
        DateTime? vehicleRegistrationExpiryDate = null,
        string? moocRegistrationNo = null,
        DateTime? moocRegistrationExpiryDate = null)
    {
        await EnsureVehicleAsync(
            vehiclePlate,
            moocNumber,
            driverName,
            transportMethod,
            ttcpWeight,
            vehicleRegistrationNo,
            vehicleRegistrationExpiryDate,
            moocRegistrationNo,
            moocRegistrationExpiryDate,
            ct);
        await EnsureCustomerAsync(customerCode, customerName, ct);
        await EnsureProductAsync(productCode, productName, productType, transactionType, ct);
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
        var now = _clock.NowLocal;

        var hasValidInputVehicleRegNo = !string.IsNullOrWhiteSpace(normalizedVehicleRegNo);
        var hasValidInputVehicleRegExpiry = vehicleRegistrationExpiryDate != null;
        var hasValidInputMoocRegNo = !string.IsNullOrWhiteSpace(normalizedMoocRegNo);
        var hasValidInputMoocRegExpiry = moocRegistrationExpiryDate != null;

        Vehicle? existing;
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

        existing ??= byPlate.FirstOrDefault();

        if (existing == null)
        {
            var fallbackVehicle = byPlate.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.VehicleRegistrationNo) || x.VehicleRegistrationExpiryDate != null);
            var byMooc = !string.IsNullOrWhiteSpace(normalizedMooc)
                ? await _vehicleRepository.GetByMoocAsync(normalizedMooc, ct)
                : Array.Empty<Vehicle>();
            var fallbackMooc = byMooc.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MoocRegistrationNo) || x.MoocRegistrationExpiryDate != null);

            existing = new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = normalizedPlate,
                MoocNumber = normalizedMooc ?? string.Empty,
                DriverName = normalizedDriver,
                TransportMethod = normalizedTransportMethod,
                TtcpWeight = ttcpWeight,
                VehicleRegistrationNo = normalizedVehicleRegNo ?? fallbackVehicle?.VehicleRegistrationNo,
                VehicleRegistrationExpiryDate = vehicleRegistrationExpiryDate ?? fallbackVehicle?.VehicleRegistrationExpiryDate,
                MoocRegistrationNo = normalizedMoocRegNo ?? fallbackMooc?.MoocRegistrationNo,
                MoocRegistrationExpiryDate = moocRegistrationExpiryDate ?? fallbackMooc?.MoocRegistrationExpiryDate,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _currentUserContext.Username
            };
            await _vehicleRepository.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Vehicle, _syncPayloadFactory.CreatePayload(existing), now, ct);
            await PropagateRegistrationInfoAsync(
                existing,
                hasValidInputVehicleRegNo ? normalizedVehicleRegNo : null,
                hasValidInputVehicleRegExpiry ? vehicleRegistrationExpiryDate : null,
                hasValidInputMoocRegNo ? normalizedMoocRegNo : null,
                hasValidInputMoocRegExpiry ? moocRegistrationExpiryDate : null,
                now,
                ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedMooc)
            && !string.Equals(existing.MoocNumber, normalizedMooc, StringComparison.OrdinalIgnoreCase))
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

        if (ttcpWeight != null && existing.TtcpWeight != ttcpWeight)
        {
            existing.TtcpWeight = ttcpWeight;
            changed = true;
        }

        if (hasValidInputVehicleRegNo
            && !string.Equals(existing.VehicleRegistrationNo, normalizedVehicleRegNo, StringComparison.Ordinal))
        {
            existing.VehicleRegistrationNo = normalizedVehicleRegNo;
            changed = true;
        }

        if (hasValidInputVehicleRegExpiry
            && existing.VehicleRegistrationExpiryDate != vehicleRegistrationExpiryDate)
        {
            existing.VehicleRegistrationExpiryDate = vehicleRegistrationExpiryDate;
            changed = true;
        }

        if (hasValidInputMoocRegNo
            && !string.Equals(existing.MoocRegistrationNo, normalizedMoocRegNo, StringComparison.Ordinal))
        {
            existing.MoocRegistrationNo = normalizedMoocRegNo;
            changed = true;
        }

        if (hasValidInputMoocRegExpiry
            && existing.MoocRegistrationExpiryDate != moocRegistrationExpiryDate)
        {
            existing.MoocRegistrationExpiryDate = moocRegistrationExpiryDate;
            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = now;
            existing.UpdatedBy = _currentUserContext.Username;
            await _vehicleRepository.UpdateAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Vehicle, _syncPayloadFactory.CreatePayload(existing), now, ct);
        }

        await PropagateRegistrationInfoAsync(
            existing,
            hasValidInputVehicleRegNo ? normalizedVehicleRegNo : null,
            hasValidInputVehicleRegExpiry ? vehicleRegistrationExpiryDate : null,
            hasValidInputMoocRegNo ? normalizedMoocRegNo : null,
            hasValidInputMoocRegExpiry ? moocRegistrationExpiryDate : null,
            now,
            ct);
    }

    private async Task EnsureCustomerAsync(string? customerCode, string? customerName, CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(customerCode);
        var normalizedName = NormalizeOptional(customerName);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var now = _clock.NowLocal;
        var existing = await _customerRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            existing = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = normalizedCode,
                CustomerName = normalizedName,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _currentUserContext.Username
            };
            await _customerRepository.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
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

        existing.UpdatedAt = now;
        existing.UpdatedBy = _currentUserContext.Username;
        await _customerRepository.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Customer, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnsureProductAsync(
        string? productCode,
        string? productName,
        string? productType,
        TransactionType transactionType,
        CancellationToken ct)
    {
        var normalizedCode = NormalizeOptional(productCode);
        var normalizedName = NormalizeOptional(productName);
        var normalizedType = ProductTypes.Normalize(productType) ?? ProductTypes.InferForTransaction(transactionType);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var now = _clock.NowLocal;
        var existing = await _productRepository.GetByCodeAsync(normalizedCode, ct);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            existing = new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = normalizedCode,
                ProductName = normalizedName,
                ProductType = normalizedType,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _currentUserContext.Username
            };
            await _productRepository.AddAsync(existing, ct);
            await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
            return;
        }

        var changed = false;
        if (!existing.IsActive)
        {
            existing.IsActive = true;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedName) && !string.Equals(existing.ProductName, normalizedName, StringComparison.Ordinal))
        {
            existing.ProductName = normalizedName;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedType) && !string.Equals(existing.ProductType, normalizedType, StringComparison.Ordinal))
        {
            existing.ProductType = normalizedType;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        existing.UpdatedAt = now;
        existing.UpdatedBy = _currentUserContext.Username;
        await _productRepository.UpdateAsync(existing, ct);
        await EnqueueMasterSyncAsync(existing.Id, SyncAggregateTypes.Product, _syncPayloadFactory.CreatePayload(existing), now, ct);
    }

    private async Task EnqueueMasterSyncAsync(
        Guid aggregateId,
        string aggregateType,
        string payloadJson,
        DateTime now,
        CancellationToken ct)
    {
        await _syncOutboxRepository.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            PayloadJson = payloadJson,
            IdempotencyKey = aggregateId,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
    }

    private async Task PropagateRegistrationInfoAsync(
        Vehicle existing,
        string? validVehicleRegNo,
        DateTime? validVehicleRegExpiryDate,
        string? validMoocRegNo,
        DateTime? validMoocRegExpiryDate,
        DateTime now,
        CancellationToken ct)
    {
        // 1. Propagate Vehicle Registration to all vehicles with the same plate
        if (validVehicleRegNo != null || validVehicleRegExpiryDate != null)
        {
            var otherVehiclesWithSamePlate = (await _vehicleRepository.GetByPlateAsync(existing.VehiclePlate, ct))
                .Where(x => x.Id != existing.Id)
                .ToList();

            foreach (var other in otherVehiclesWithSamePlate)
            {
                var otherChanged = false;
                if (validVehicleRegNo != null && other.VehicleRegistrationNo != validVehicleRegNo)
                {
                    other.VehicleRegistrationNo = validVehicleRegNo;
                    otherChanged = true;
                }
                if (validVehicleRegExpiryDate != null && other.VehicleRegistrationExpiryDate != validVehicleRegExpiryDate)
                {
                    other.VehicleRegistrationExpiryDate = validVehicleRegExpiryDate;
                    otherChanged = true;
                }
                if (otherChanged)
                {
                    other.UpdatedAt = now;
                    other.UpdatedBy = _currentUserContext.Username;
                    await _vehicleRepository.UpdateAsync(other, ct);
                    await EnqueueMasterSyncAsync(other.Id, SyncAggregateTypes.Vehicle, _syncPayloadFactory.CreatePayload(other), now, ct);
                }
            }
        }

        // 2. Propagate Mooc Registration to all vehicles with the same Mooc number
        if (!string.IsNullOrWhiteSpace(existing.MoocNumber) && (validMoocRegNo != null || validMoocRegExpiryDate != null))
        {
            var otherVehiclesWithSameMooc = (await _vehicleRepository.GetByMoocAsync(existing.MoocNumber, ct))
                .Where(x => x.Id != existing.Id)
                .ToList();

            foreach (var other in otherVehiclesWithSameMooc)
            {
                var otherChanged = false;
                if (validMoocRegNo != null && other.MoocRegistrationNo != validMoocRegNo)
                {
                    other.MoocRegistrationNo = validMoocRegNo;
                    otherChanged = true;
                }
                if (validMoocRegExpiryDate != null && other.MoocRegistrationExpiryDate != validMoocRegExpiryDate)
                {
                    other.MoocRegistrationExpiryDate = validMoocRegExpiryDate;
                    otherChanged = true;
                }
                if (otherChanged)
                {
                    other.UpdatedAt = now;
                    other.UpdatedBy = _currentUserContext.Username;
                    await _vehicleRepository.UpdateAsync(other, ct);
                    await EnqueueMasterSyncAsync(other.Id, SyncAggregateTypes.Vehicle, _syncPayloadFactory.CreatePayload(other), now, ct);
                }
            }
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
