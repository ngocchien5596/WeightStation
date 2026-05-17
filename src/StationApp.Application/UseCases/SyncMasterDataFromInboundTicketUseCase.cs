using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public class SyncMasterDataFromInboundTicketUseCase
{
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly IUnitOfWork _uow;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly ISyncPayloadFactory _payloadFactory;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUser;

    public SyncMasterDataFromInboundTicketUseCase(
        IVehicleRepository vehicleRepo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        IUnitOfWork uow,
        ISyncOutboxRepository outboxRepo,
        ISyncPayloadFactory payloadFactory,
        IClock clock,
        ICurrentUserContext currentUser)
    {
        _vehicleRepo = vehicleRepo;
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _uow = uow;
        _outboxRepo = outboxRepo;
        _payloadFactory = payloadFactory;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(WeighTicket ticket, CancellationToken ct)
    {
        // 1. Upsert Vehicle
        var vehicle = await UpsertVehicleAsync(ticket.VehiclePlate, ticket.MoocNumber, ticket.DriverName, ticket.TransportMethod, ct);
        
        // 2. Upsert Customer
        var customer = await UpsertCustomerAsync(ticket.CustomerCode, ticket.CustomerName, ct);
        
        // 3. Upsert Product
        var product = await UpsertProductAsync(ticket.ProductCode, ticket.ProductName, ct);

        if (vehicle != null)
        {
            if (!string.IsNullOrEmpty(vehicle.DriverName)) ticket.DriverName = vehicle.DriverName;
            if (!string.IsNullOrEmpty(vehicle.TransportMethod)) ticket.TransportMethod = Enum.TryParse<TransportMethod>(vehicle.TransportMethod, out var tm) ? tm : ticket.TransportMethod;
        }

        if (customer != null && !string.IsNullOrEmpty(customer.CustomerName)) ticket.CustomerName = customer.CustomerName;
        if (product != null && !string.IsNullOrEmpty(product.ProductName)) ticket.ProductName = product.ProductName;

        await _uow.SaveChangesAsync(ct);
        await EnqueueMasterSyncAsync(vehicle, customer, product, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ExecuteAsync(VehicleRegistration reg, CancellationToken ct)
    {
        var vehicle = await UpsertVehicleAsync(reg.VehiclePlate, reg.MoocNumber, reg.ReceiverName, reg.TransportMethod, ct);
        var customer = await UpsertCustomerAsync(reg.CustomerCode, reg.CustomerName, ct);
        var product = await UpsertProductAsync(reg.ProductCode, reg.ProductName, ct);

        if (vehicle != null)
        {
            if (!string.IsNullOrEmpty(vehicle.DriverName)) reg.ReceiverName = vehicle.DriverName;
            if (!string.IsNullOrEmpty(vehicle.TransportMethod)) reg.TransportMethod = Enum.TryParse<TransportMethod>(vehicle.TransportMethod, out var tm) ? tm : reg.TransportMethod;
        }

        if (customer != null && !string.IsNullOrEmpty(customer.CustomerName)) reg.CustomerName = customer.CustomerName;
        if (product != null && !string.IsNullOrEmpty(product.ProductName)) reg.ProductName = product.ProductName;

        await _uow.SaveChangesAsync(ct);
        await EnqueueMasterSyncAsync(vehicle, customer, product, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task EnqueueMasterSyncAsync(Vehicle? vehicle, Customer? customer, Product? product, CancellationToken ct)
    {
        var now = _clock.NowLocal;

        if (vehicle != null)
        {
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = vehicle.Id,
                AggregateType = SyncAggregateTypes.Vehicle,
                PayloadJson = _payloadFactory.CreatePayload(vehicle),
                IdempotencyKey = vehicle.Id,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }

        if (customer != null)
        {
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = customer.Id,
                AggregateType = SyncAggregateTypes.Customer,
                PayloadJson = _payloadFactory.CreatePayload(customer),
                IdempotencyKey = customer.Id,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }

        if (product != null)
        {
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = product.Id,
                AggregateType = SyncAggregateTypes.Product,
                PayloadJson = _payloadFactory.CreatePayload(product),
                IdempotencyKey = product.Id,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }
    }

    private async Task<Vehicle?> UpsertVehicleAsync(string? plate, string? mooc, string? driver, Domain.Enums.TransportMethod? transportMethod, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(plate)) return null;
        mooc ??= "";

        var vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(plate, mooc, ct);
        if (vehicle == null)
        {
            vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = plate,
                MoocNumber = mooc,
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _vehicleRepo.AddAsync(vehicle, ct);
        }

        if (!string.IsNullOrEmpty(driver)) vehicle.DriverName = driver;
        if (transportMethod.HasValue) vehicle.TransportMethod = transportMethod.ToString();

        vehicle.UpdatedAt = _clock.NowLocal;
        vehicle.UpdatedBy = _currentUser.Username ?? "SYSTEM";

        await _vehicleRepo.UpdateAsync(vehicle, ct);
        return vehicle;
    }

    private async Task<Customer?> UpsertCustomerAsync(string? code, string? name, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code)) return null;

        var customer = await _customerRepo.GetByCodeAsync(code, ct);
        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = code,
                CustomerName = name ?? "",
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _customerRepo.AddAsync(customer, ct);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            customer.CustomerName = name;
            customer.UpdatedAt = _clock.NowLocal;
            customer.UpdatedBy = _currentUser.Username ?? "SYSTEM";
            await _customerRepo.UpdateAsync(customer, ct);
        }

        return customer;
    }

    private async Task<Product?> UpsertProductAsync(string? code, string? name, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code)) return null;

        var product = await _productRepo.GetByCodeAsync(code, ct);
        if (product == null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = code,
                ProductName = name ?? "",
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _productRepo.AddAsync(product, ct);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            product.ProductName = name;
            product.UpdatedAt = _clock.NowLocal;
            product.UpdatedBy = _currentUser.Username ?? "SYSTEM";
            await _productRepo.UpdateAsync(product, ct);
        }

        return product;
    }

    private async Task<Vehicle?> UpsertVehicleAsync(WeighTicket ticket, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ticket.VehiclePlate)) return null;

        var plate = ticket.VehiclePlate;
        var mooc = ticket.MoocNumber ?? "";

        var vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(plate, mooc, ct);
        if (vehicle == null)
        {
            vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                VehiclePlate = plate,
                MoocNumber = mooc,
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _vehicleRepo.AddAsync(vehicle, ct);
        }

        // Update fields if payload not empty
        if (!string.IsNullOrEmpty(ticket.DriverName)) vehicle.DriverName = ticket.DriverName;
        if (ticket.TransportMethod.HasValue) vehicle.TransportMethod = ticket.TransportMethod.ToString();
        
        // These fields are coming from the ticket delta / inbound payload
        // In a real scenario, the inbound DTO would have these. 
        // For now we assume they are populated in the ticket entity during inbound processing.
        if (ticket.Ttcp10WeightSnapshot.HasValue) vehicle.TtcpWeight = ticket.Ttcp10WeightSnapshot;
        if (!string.IsNullOrEmpty(ticket.VehicleRegistrationNoSnapshot)) vehicle.VehicleRegistrationNo = ticket.VehicleRegistrationNoSnapshot;
        if (ticket.VehicleRegistrationExpirySnapshot.HasValue) vehicle.VehicleRegistrationExpiryDate = ticket.VehicleRegistrationExpirySnapshot;
        if (!string.IsNullOrEmpty(ticket.MoocRegistrationNoSnapshot)) vehicle.MoocRegistrationNo = ticket.MoocRegistrationNoSnapshot;
        if (ticket.MoocRegistrationExpirySnapshot.HasValue) vehicle.MoocRegistrationExpiryDate = ticket.MoocRegistrationExpirySnapshot;

        vehicle.UpdatedAt = _clock.NowLocal;
        vehicle.UpdatedBy = _currentUser.Username ?? "SYSTEM";

        await _vehicleRepo.UpdateAsync(vehicle, ct);
        return vehicle;
    }

    private async Task<Customer?> UpsertCustomerAsync(WeighTicket ticket, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ticket.CustomerCode)) return null;

        var customer = await _customerRepo.GetByCodeAsync(ticket.CustomerCode, ct);
        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = ticket.CustomerCode,
                CustomerName = ticket.CustomerName ?? "",
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _customerRepo.AddAsync(customer, ct);
        }
        else if (!string.IsNullOrEmpty(ticket.CustomerName))
        {
            customer.CustomerName = ticket.CustomerName;
            customer.UpdatedAt = _clock.NowLocal;
            customer.UpdatedBy = _currentUser.Username ?? "SYSTEM";
            await _customerRepo.UpdateAsync(customer, ct);
        }

        return customer;
    }

    private async Task<Product?> UpsertProductAsync(WeighTicket ticket, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ticket.ProductCode)) return null;

        var product = await _productRepo.GetByCodeAsync(ticket.ProductCode, ct);
        if (product == null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                ProductCode = ticket.ProductCode,
                ProductName = ticket.ProductName ?? "",
                CreatedAt = _clock.NowLocal,
                CreatedBy = _currentUser.Username ?? "SYSTEM"
            };
            await _productRepo.AddAsync(product, ct);
        }
        else if (!string.IsNullOrEmpty(ticket.ProductName))
        {
            product.ProductName = ticket.ProductName;
            product.UpdatedAt = _clock.NowLocal;
            product.UpdatedBy = _currentUser.Username ?? "SYSTEM";
            await _productRepo.UpdateAsync(product, ct);
        }

        return product;
    }
}
