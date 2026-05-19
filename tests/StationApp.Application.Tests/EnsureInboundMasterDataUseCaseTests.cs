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
}
