using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Sync.Services;
using Xunit;

namespace StationApp.Sync.Tests;

public class VehicleRegistrationInboundProcessorTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VehicleRegistrationInboundProcessor> _logger;

    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IProductRepository _productRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IClock _clock;
    private readonly IAppConfigRepository _appConfig;

    public VehicleRegistrationInboundProcessorTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scope = Substitute.For<IServiceScope>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<VehicleRegistrationInboundProcessor>>();

        _regRepo = Substitute.For<IVehicleRegistrationRepository>();
        _vehicleRepo = Substitute.For<IVehicleRepository>();
        _customerRepo = Substitute.For<ICustomerRepository>();
        _productRepo = Substitute.For<IProductRepository>();
        _uow = Substitute.For<IUnitOfWork>();
        _audit = Substitute.For<IAuditService>();
        _clock = Substitute.For<IClock>();
        _appConfig = Substitute.For<IAppConfigRepository>();

        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);

        _serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_uow);
        _serviceProvider.GetService(typeof(IVehicleRegistrationRepository)).Returns(_regRepo);
        _serviceProvider.GetService(typeof(IVehicleRepository)).Returns(_vehicleRepo);
        _serviceProvider.GetService(typeof(ICustomerRepository)).Returns(_customerRepo);
        _serviceProvider.GetService(typeof(IProductRepository)).Returns(_productRepo);
        _serviceProvider.GetService(typeof(IAuditService)).Returns(_audit);
        _serviceProvider.GetService(typeof(IClock)).Returns(_clock);
        _serviceProvider.GetService(typeof(IAppConfigRepository)).Returns(_appConfig);

        _clock.NowLocal.Returns(new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task T1_Process_HappyPath_UpdatesStatusAndUpsertsMasters()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.ERP,
            VehiclePlate = " 30A-12345 ",
            CustomerCode = "C001",
            CustomerName = "Customer 1",
            ProductCode = "P001",
            ProductName = "Product 1",
            IsInboundProcessed = false
        };

        _regRepo.GetUnprocessedInboundAsync(Arg.Any<CancellationToken>())
            .Returns(new List<VehicleRegistration> { registration });

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                await action(callInfo.ArgAt<CancellationToken>(1));
            });

        var processor = new TestableVehicleRegistrationInboundProcessor(_scopeFactory, _logger);

        await processor.RunCycleAsync(CancellationToken.None);

        Assert.True(registration.IsInboundProcessed);
        Assert.Equal(RegistrationStatus.REGISTERED, registration.RegistrationStatus);
        Assert.Equal("30A-12345", registration.VehiclePlate);

        await _vehicleRepo.Received(1).AddAsync(Arg.Is<Vehicle>(v => v.VehiclePlate == "30A-12345"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task T2_3_4_MasterExisted_DoesNotDuplicate()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.ERP,
            VehiclePlate = "30A-12345",
            CustomerCode = "C001",
            ProductCode = "P001"
        };

        var existingVehicle = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "30A-12345" };
        var existingCustomer = new Customer { Id = Guid.NewGuid(), CustomerCode = "C001" };
        var existingProduct = new Product { Id = Guid.NewGuid(), ProductCode = "P001" };

        _regRepo.GetUnprocessedInboundAsync(Arg.Any<CancellationToken>()).Returns(new List<VehicleRegistration> { registration });
        _vehicleRepo.GetByPlateAndMoocAsync("30A-12345", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(existingVehicle);
        _customerRepo.GetByCodeAsync("C001", Arg.Any<CancellationToken>()).Returns(existingCustomer);
        _productRepo.GetByCodeAsync("P001", Arg.Any<CancellationToken>()).Returns(existingProduct);

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                await action(callInfo.ArgAt<CancellationToken>(1));
            });

        var processor = new TestableVehicleRegistrationInboundProcessor(_scopeFactory, _logger);

        await processor.RunCycleAsync(CancellationToken.None);

        await _vehicleRepo.DidNotReceive().AddAsync(Arg.Any<Vehicle>(), Arg.Any<CancellationToken>());
        await _customerRepo.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await _productRepo.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        Assert.True(registration.IsInboundProcessed);
    }

    [Fact]
    public async Task T5_ValidationFailed_LogsAndDoesNotUpsert()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.ERP,
            VehiclePlate = "   ", 
            CustomerCode = "C001",
            ProductCode = "P001"
        };

        _regRepo.GetUnprocessedInboundAsync(Arg.Any<CancellationToken>()).Returns(new List<VehicleRegistration> { registration });

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                await action(callInfo.ArgAt<CancellationToken>(1));
            });

        var processor = new TestableVehicleRegistrationInboundProcessor(_scopeFactory, _logger);

        await processor.RunCycleAsync(CancellationToken.None);

        Assert.False(registration.IsInboundProcessed);
        Assert.Equal("VALIDATION_FAILED", registration.InboundErrorCode);
        await _audit.Received(1).LogAsync("ERP_INBOUND_VALIDATION_FAILED", nameof(VehicleRegistration), registration.Id, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task T6_CancelledValidRecord_SetsCancelled()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.ERP,
            VehiclePlate = "30A-12345",
            CustomerCode = "C001",
            ProductCode = "P001",
            IsCancelled = true
        };

        _regRepo.GetUnprocessedInboundAsync(Arg.Any<CancellationToken>()).Returns(new List<VehicleRegistration> { registration });

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                await action(callInfo.ArgAt<CancellationToken>(1));
            });

        var processor = new TestableVehicleRegistrationInboundProcessor(_scopeFactory, _logger);

        await processor.RunCycleAsync(CancellationToken.None);

        Assert.True(registration.IsInboundProcessed);
        Assert.Equal(RegistrationStatus.CANCELLED, registration.RegistrationStatus);
        await _vehicleRepo.Received(1).AddAsync(Arg.Any<Vehicle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task T7_CancelledInvalidRecord_Rejects()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            RegistrationSource = RegistrationSource.ERP,
            VehiclePlate = "", 
            CustomerCode = "C001",
            ProductCode = "P001",
            IsCancelled = true
        };

        _regRepo.GetUnprocessedInboundAsync(Arg.Any<CancellationToken>()).Returns(new List<VehicleRegistration> { registration });

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var action = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                await action(callInfo.ArgAt<CancellationToken>(1));
            });

        var processor = new TestableVehicleRegistrationInboundProcessor(_scopeFactory, _logger);

        await processor.RunCycleAsync(CancellationToken.None);

        Assert.False(registration.IsInboundProcessed);
        await _audit.Received(1).LogAsync("ERP_INBOUND_VALIDATION_FAILED", nameof(VehicleRegistration), registration.Id, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    private class TestableVehicleRegistrationInboundProcessor : VehicleRegistrationInboundProcessor
    {
        public TestableVehicleRegistrationInboundProcessor(IServiceScopeFactory scopeFactory, ILogger<VehicleRegistrationInboundProcessor> logger)
            : base(scopeFactory, logger)
        {
        }

        public async Task RunCycleAsync(CancellationToken ct)
        {
            var method = typeof(VehicleRegistrationInboundProcessor)
                .GetMethod("ProcessPendingInboundAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)method!.Invoke(this, new object[] { ct })!;
        }
    }
}
