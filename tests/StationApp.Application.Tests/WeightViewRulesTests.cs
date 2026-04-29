using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class WeightViewRulesTests
{
    [Fact]
    public async Task SplitOverweightTicket_KeepsFullLoadTicketAsPrimaryDisplay()
    {
        var regRepo = Substitute.For<IVehicleRegistrationRepository>();
        var ticketRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var outboxRepo = Substitute.For<ISyncOutboxRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var versionProvider = Substitute.For<IAppVersionProvider>();
        var userContext = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var audit = Substitute.For<IAuditService>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var configRepo = Substitute.For<IAppConfigRepository>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();

        var now = new DateTime(2026, 4, 27, 11, 0, 0);
        var registrationId = Guid.NewGuid();
        var ticket1Id = Guid.NewGuid();
        var delivery1Id = Guid.NewGuid();
        WeighTicket? addedTicket2 = null;
        DeliveryTicket? addedDelivery2 = null;

        var registration = new VehicleRegistration
        {
            Id = registrationId,
            VehiclePlate = "51C-12345",
            MoocNumber = "51R-99999",
            RegistrationStatus = RegistrationStatus.LOADING_IN_PROGRESS,
            TransactionType = TransactionType.OUTBOUND,
            TransportMethod = TransportMethod.ROAD,
            PlannedWeight = 20000,
            CurrentPrimaryWeighTicketId = ticket1Id,
            CurrentPrimaryDeliveryTicketId = delivery1Id,
            IdempotencyKey = Guid.NewGuid()
        };

        var ticket1 = new WeighTicket
        {
            Id = ticket1Id,
            VehicleRegistrationId = registrationId,
            TicketNo = "QN000001",
            Weight1 = 10000,
            Status = TicketStatus.LOADING_STARTED,
            IsPrimaryDisplay = true,
            RecordRole = "WORKING",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = now.AddMinutes(-30),
            CreatedBy = "tester"
        };

        var delivery1 = new DeliveryTicket
        {
            Id = delivery1Id,
            VehicleRegistrationId = registrationId,
            DeliveryNo = "PGN000001",
            RecordRole = "WORKING",
            CreatedAt = now.AddMinutes(-30),
            CreatedBy = "tester"
        };

        regRepo.GetByIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(registration);
        ticketRepo.GetPrimaryByVehicleRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(ticket1);
        deliveryRepo.GetPrimaryByVehicleRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(delivery1);
        configRepo.GetValueAsync("overweight_split_residual_ratio", Arg.Any<CancellationToken>()).Returns("0");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("QN000002");
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("PGN000002");
        versionProvider.GetVersion().Returns("1.0.0");
        userContext.Username.Returns("tester");
        clock.NowLocal.Returns(now);
        payloadFactory.CreatePayload(Arg.Any<WeighTicket>()).Returns("{}");
        payloadFactory.CreatePayload(Arg.Any<VehicleRegistration>()).Returns("{}");
        vehicleRepo.GetByPlateAndMoocAsync(registration.VehiclePlate, registration.MoocNumber!, Arg.Any<CancellationToken>())
            .Returns(new Vehicle
            {
                VehiclePlate = registration.VehiclePlate,
                MoocNumber = registration.MoocNumber!,
                TtcpWeight = 20000
            });

        ticketRepo
            .When(repo => repo.AddAsync(Arg.Any<WeighTicket>(), Arg.Any<CancellationToken>()))
            .Do(call => addedTicket2 = call.Arg<WeighTicket>());
        deliveryRepo
            .When(repo => repo.AddAsync(Arg.Any<DeliveryTicket>(), Arg.Any<CancellationToken>()))
            .Do(call => addedDelivery2 = call.Arg<DeliveryTicket>());
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0])(CancellationToken.None));

        var useCase = new SplitOverweightTicketUseCase(
            regRepo,
            ticketRepo,
            deliveryRepo,
            outboxRepo,
            uow,
            versionProvider,
            userContext,
            clock,
            audit,
            payloadFactory,
            configRepo,
            ticketNoGen,
            deliveryNoGen,
            vehicleRepo);

        var result = await useCase.ExecuteAsync(
            new SplitOverweightTicketRequest(registrationId, 35000, true, WeightMode.MANUAL),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(addedTicket2);
        Assert.NotNull(addedDelivery2);

        Assert.Equal(ticket1.Id, registration.CurrentPrimaryWeighTicketId);
        Assert.Equal(delivery1.Id, registration.CurrentPrimaryDeliveryTicketId);

        Assert.True(ticket1.IsPrimaryDisplay);
        Assert.Equal((byte)1, ticket1.SplitSequence);
        Assert.Equal(TicketStatus.TICKET_COMPLETED, ticket1.Status);
        Assert.Equal(22000, ticket1.NetWeight);

        Assert.False(addedTicket2!.IsPrimaryDisplay);
        Assert.Equal((byte)2, addedTicket2.SplitSequence);
        Assert.Equal(ticket1.Id, addedTicket2.SourceTicketId);
        Assert.Equal(3000, addedTicket2.NetWeight);
    }

    [Fact]
    public async Task GetRelatedTickets_ReturnsWeighAndDeliveryDocuments()
    {
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var registrationId = Guid.NewGuid();

        weighRepo.GetByVehicleRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(
            new List<WeighTicket>
            {
                new()
                {
                    VehicleRegistrationId = registrationId,
                    TicketNo = "QN000001",
                    RecordRole = "WORKING",
                    SplitSequence = 1,
                    Weight1 = 10000,
                    Weight2 = 32000,
                    NetWeight = 22000,
                    CreatedAt = new DateTime(2026, 4, 27, 10, 0, 0)
                }
            });

        deliveryRepo.GetByVehicleRegistrationIdAsync(registrationId, Arg.Any<CancellationToken>()).Returns(
            new List<DeliveryTicket>
            {
                new()
                {
                    VehicleRegistrationId = registrationId,
                    DeliveryNo = "PGN000001",
                    RecordRole = "WORKING",
                    SplitSequence = 1,
                    CreatedAt = new DateTime(2026, 4, 27, 10, 5, 0)
                }
            });

        var useCase = new GetRelatedTicketsUseCase(weighRepo, deliveryRepo);

        var result = await useCase.ExecuteAsync(registrationId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.DocumentType == "PHIEU CAN" && item.TicketNo == "QN000001");
        Assert.Contains(result, item => item.DocumentType == "PHIEU GIAO NHAN" && item.DeliveryNo == "PGN000001");
    }
}
