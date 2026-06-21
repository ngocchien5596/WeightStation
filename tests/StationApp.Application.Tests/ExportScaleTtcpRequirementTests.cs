using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class ExportScaleTtcpRequirementTests
{
    [Fact]
    public async Task CaptureSessionWeight1_AllowsExportScaleSessionWithoutTtcp_WhenOutboundTtcpIsRequired()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var complianceSettingsProvider = Substitute.For<IIncomingVehicleComplianceSettingsProvider>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 6, 21, 8, 0, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = "14C-12345",
            MoocNumber = "14R-12345"
        };
        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            CreatedAt = now.AddMinutes(-5),
            TransactionType = TransactionType.OUTBOUND,
            IsExportScale = true,
            ProductType = ProductTypes.Bagged,
            ProductCode = "PCB40",
            ProductName = "Xi mang bao",
            CustomerCode = "C001",
            CustomerName = "Khach hang 1",
            PlannedWeight = 10_000m,
            BagCount = 200,
            TransportMethod = TransportMethod.ROAD
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = cutOrder.Id,
            PlannedWeight = 10_000m
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        cutOrderRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { cutOrder });
        vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, session.MoocNumber, Arg.Any<CancellationToken>()).Returns(
            new Vehicle
            {
                VehiclePlate = session.VehiclePlate,
                MoocNumber = session.MoocNumber
            });
        vehicleRepo.GetByPlateAsync(session.VehiclePlate, Arg.Any<CancellationToken>()).Returns(Array.Empty<Vehicle>());
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns((WeighTicket?)null);
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("WT-0001");
        complianceSettingsProvider.GetCurrentRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new IncomingVehicleComplianceRules(
                new IncomingVehicleComplianceRuleSet(true, false),
                new IncomingVehicleComplianceRuleSet(true, false)));
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CaptureSessionWeight1UseCase(
            sessionRepo,
            cutOrderRepo,
            vehicleRepo,
            weighRepo,
            Substitute.For<IWeighingSessionImageRepository>(),
            Substitute.For<ICameraSettingsProvider>(),
            Substitute.For<ICameraCaptureService>(),
            new WeighingSessionTicketSyncService(),
            ticketNoGen,
            complianceSettingsProvider,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(session.Id, 12_000m, true, WeightMode.AUTO),
            CancellationToken.None);

        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, session.SessionStatus);
        Assert.Null(session.Ttcp10WeightSnapshot);
        await weighRepo.Received(1).AddAsync(
            Arg.Is<WeighTicket>(x => x.CutOrderId == cutOrder.Id && x.Ttcp10WeightSnapshot == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateExportVehicleSession_DoesNotClearExistingVehicleTtcp_WhenExportRequestHasNoTtcp()
    {
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 6, 21, 9, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.OUTBOUND,
            IsExportScale = true,
            IsTemporaryExport = true,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            ProcessingStage = ProcessingStage.IN_YARD,
            ProductCode = "PCB40",
            ProductName = "Xi mang bao",
            CustomerCode = "C001",
            CustomerName = "Khach hang 1",
            PlannedWeight = 20_000m,
            BagCount = 400
        };
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "14C-12345",
            MoocNumber = string.Empty,
            TtcpWeight = 18_000m
        };

        cutOrderRepo.GetByIdAsync(cutOrder.Id, Arg.Any<CancellationToken>()).Returns(cutOrder);
        cutOrderRepo.GetActiveExportScaleCutOrdersAsync(Arg.Any<ExportScaleCutOrderFilter>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExportScaleCutOrderListItem>());
        vehicleRepo.GetByPlateAsync("14C-12345", Arg.Any<CancellationToken>()).Returns(new[] { vehicle });
        sessionNoGen.GenerateAsync(TransactionType.OUTBOUND, Arg.Any<CancellationToken>()).Returns("LC26060001");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CreateExportVehicleSessionUseCase(
            cutOrderRepo,
            sessionRepo,
            vehicleRepo,
            sessionNoGen,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CreateExportVehicleSessionRequest(
                cutOrder.Id,
                "14C-12345",
                null,
                "Tai xe 1",
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        await vehicleRepo.Received(1).UpdateAsync(
            Arg.Is<Vehicle>(x => x.Id == vehicle.Id && x.TtcpWeight == 18_000m),
            Arg.Any<CancellationToken>());
    }
}
