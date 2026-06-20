using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases.MasterData;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class EnsureInboundMasterDataUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesExistingVehicleTtcpWeightWhenIncomingFormChangesValue()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var outboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var now = new DateTime(2026, 6, 18, 22, 0, 0);

        clock.NowLocal.Returns(now);
        currentUser.Username.Returns("tester");
        payloadFactory.CreatePayload(Arg.Any<Vehicle>()).Returns("{}");

        var existingVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14H-04022",
            MoocNumber = string.Empty,
            TtcpWeight = 5000m,
            IsActive = true,
            CreatedAt = now.AddDays(-5),
            CreatedBy = "seed"
        };

        vehicleRepo.GetByPlateAsync("14H-04022", Arg.Any<CancellationToken>())
            .Returns(new[] { existingVehicle });

        var sut = new EnsureInboundMasterDataUseCase(
            vehicleRepo,
            customerRepo,
            productRepo,
            outboxRepo,
            payloadFactory,
            clock,
            currentUser);

        await sut.ExecuteAsync(
            vehiclePlate: "14H-04022",
            moocNumber: null,
            driverName: null,
            transportMethod: TransportMethod.ROAD,
            customerCode: null,
            customerName: null,
            productCode: null,
            productName: null,
            productType: null,
            transactionType: TransactionType.INBOUND,
            ct: CancellationToken.None,
            ttcpWeight: 6000m,
            vehicleRegistrationNo: null,
            vehicleRegistrationExpiryDate: null,
            moocRegistrationNo: null,
            moocRegistrationExpiryDate: null);

        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x =>
                x.Id == existingVehicle.Id &&
                x.TtcpWeight == 6000m &&
                x.UpdatedAt == now &&
                x.UpdatedBy == "tester"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesVehicleRegistrationFieldsWhenIncomingFormProvidesNewValues()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var outboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var now = new DateTime(2026, 5, 19, 15, 30, 0);

        clock.NowLocal.Returns(now);
        currentUser.Username.Returns("tester");
        payloadFactory.CreatePayload(Arg.Any<Vehicle>()).Returns("{}");

        var existingVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-2692",
            MoocNumber = "MOOC-OLD",
            VehicleRegistrationNo = "DKX-OLD",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OLD",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        vehicleRepo.GetByPlateAsync("14C-2692", Arg.Any<CancellationToken>())
            .Returns(new[] { existingVehicle });

        var sut = new EnsureInboundMasterDataUseCase(
            vehicleRepo,
            customerRepo,
            productRepo,
            outboxRepo,
            payloadFactory,
            clock,
            currentUser);

        await sut.ExecuteAsync(
            vehiclePlate: "14C-2692",
            moocNumber: "MOOC-NEW",
            driverName: "Driver",
            transportMethod: TransportMethod.ROAD,
            customerCode: null,
            customerName: null,
            productCode: null,
            productName: null,
            productType: null,
            transactionType: TransactionType.OUTBOUND,
            ct: CancellationToken.None,
            ttcpWeight: null,
            vehicleRegistrationNo: "DKX-NEW",
            vehicleRegistrationExpiryDate: new DateTime(2027, 1, 1),
            moocRegistrationNo: "DKM-NEW",
            moocRegistrationExpiryDate: new DateTime(2027, 2, 1));

        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x =>
                x.VehicleRegistrationNo == "DKX-NEW" &&
                x.VehicleRegistrationExpiryDate == new DateTime(2027, 1, 1) &&
                x.MoocRegistrationNo == "DKM-NEW" &&
                x.MoocRegistrationExpiryDate == new DateTime(2027, 2, 1) &&
                x.UpdatedAt == now &&
                x.UpdatedBy == "tester"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesRegistrationFieldsToOtherVehiclesWithSamePlateOrMooc()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var outboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var now = new DateTime(2026, 5, 19, 15, 30, 0);

        clock.NowLocal.Returns(now);
        currentUser.Username.Returns("tester");
        payloadFactory.CreatePayload(Arg.Any<Vehicle>()).Returns("{}");

        var vehicle1 = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-2692",
            MoocNumber = "MOOC-1",
            VehicleRegistrationNo = "DKX-OLD",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OLD",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        var vehicle2 = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-2692",
            MoocNumber = "MOOC-2",
            VehicleRegistrationNo = "DKX-OLD",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OTHER",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        var vehicle3 = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-9999",
            MoocNumber = "MOOC-1",
            VehicleRegistrationNo = "DKX-OTHER",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OLD",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        // Mock queries
        vehicleRepo.GetByPlateAsync("14C-2692", Arg.Any<CancellationToken>())
            .Returns(new[] { vehicle1, vehicle2 });
        vehicleRepo.GetByMoocAsync("MOOC-1", Arg.Any<CancellationToken>())
            .Returns(new[] { vehicle1, vehicle3 });

        var sut = new EnsureInboundMasterDataUseCase(
            vehicleRepo,
            customerRepo,
            productRepo,
            outboxRepo,
            payloadFactory,
            clock,
            currentUser);

        await sut.ExecuteAsync(
            vehiclePlate: "14C-2692",
            moocNumber: "MOOC-1",
            driverName: "Driver",
            transportMethod: TransportMethod.ROAD,
            customerCode: null,
            customerName: null,
            productCode: null,
            productName: null,
            productType: null,
            transactionType: TransactionType.OUTBOUND,
            ct: CancellationToken.None,
            ttcpWeight: null,
            vehicleRegistrationNo: "DKX-NEW",
            vehicleRegistrationExpiryDate: new DateTime(2027, 1, 1),
            moocRegistrationNo: "DKM-NEW",
            moocRegistrationExpiryDate: new DateTime(2027, 2, 1));

        // Verify vehicle1 was updated
        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle1.Id && x.VehicleRegistrationNo == "DKX-NEW" && x.MoocRegistrationNo == "DKM-NEW"),
            Arg.Any<CancellationToken>());

        // Verify vehicle2 (same plate) got vehicle registration propagated
        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle2.Id && x.VehicleRegistrationNo == "DKX-NEW" && x.MoocRegistrationNo == "DKM-OTHER"),
            Arg.Any<CancellationToken>());

        // Verify vehicle3 (same Mooc) got Mooc registration propagated
        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle3.Id && x.VehicleRegistrationNo == "DKX-OTHER" && x.MoocRegistrationNo == "DKM-NEW"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPropagateRegistrationFieldsToOtherVehiclesWhenIncomingFormDoesNotProvideRegistrationValues()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var outboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var now = new DateTime(2026, 5, 19, 15, 30, 0);

        clock.NowLocal.Returns(now);
        currentUser.Username.Returns("tester");
        payloadFactory.CreatePayload(Arg.Any<Vehicle>()).Returns("{}");

        var vehicle1 = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-2692",
            MoocNumber = "MOOC-1",
            VehicleRegistrationNo = "DKX-OLD",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OLD",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        var vehicle2 = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-2692",
            MoocNumber = "MOOC-2",
            VehicleRegistrationNo = "DKX-OLD",
            VehicleRegistrationExpiryDate = new DateTime(2026, 1, 1),
            MoocRegistrationNo = "DKM-OTHER",
            MoocRegistrationExpiryDate = new DateTime(2026, 2, 1),
            IsActive = true,
            CreatedAt = now.AddDays(-10),
            CreatedBy = "seed"
        };

        // Mock queries
        vehicleRepo.GetByPlateAsync("14C-2692", Arg.Any<CancellationToken>())
            .Returns(new[] { vehicle1, vehicle2 });

        var sut = new EnsureInboundMasterDataUseCase(
            vehicleRepo,
            customerRepo,
            productRepo,
            outboxRepo,
            payloadFactory,
            clock,
            currentUser);

        await sut.ExecuteAsync(
            vehiclePlate: "14C-2692",
            moocNumber: "MOOC-1",
            driverName: "New Driver Name", // Trigger changed = true to call the flow
            transportMethod: TransportMethod.ROAD,
            customerCode: null,
            customerName: null,
            productCode: null,
            productName: null,
            productType: null,
            transactionType: TransactionType.OUTBOUND,
            ct: CancellationToken.None,
            ttcpWeight: null,
            vehicleRegistrationNo: null,
            vehicleRegistrationExpiryDate: null,
            moocRegistrationNo: null,
            moocRegistrationExpiryDate: null);

        // Verify vehicle1 was updated for DriverName
        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle1.Id && x.DriverName == "New Driver Name"),
            Arg.Any<CancellationToken>());

        // Verify vehicle2 (same plate) did NOT receive any update calls because no valid registration input was provided
        await vehicleRepo.DidNotReceive().UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle2.Id),
            Arg.Any<CancellationToken>());
    }
}
