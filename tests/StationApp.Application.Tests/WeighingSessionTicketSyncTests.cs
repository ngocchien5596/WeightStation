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

public class WeighingSessionTicketSyncTests
{
    [Fact]
    public async Task CreateWeighingSession_AppliesCarryForwardWeight1AndCreatesMasterTicket()
    {
        var regRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var now = new DateTime(2026, 5, 20, 9, 0, 0);
        var weight1Time = now.AddMinutes(-25);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = "QN.CL.2605/9999",
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            ProcessingStage = ProcessingStage.IN_YARD,
            VehiclePlate = "14H-0032",
            ReceiverName = "Driver A",
            CustomerCode = "C1",
            CustomerName = "Customer 1",
            ProductCode = "XM",
            ProductName = "Xi mang",
            PlannedWeight = 10_000m,
            BagCount = 200,
            CreatedAt = now.AddMinutes(-40),
            CarryForwardWeight1 = 12_500m,
            CarryForwardWeight1Time = weight1Time,
            TransportMethod = TransportMethod.ROAD
        };

        regRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { cutOrder });
        vehicleRepo.GetByPlateAndMoocAsync(cutOrder.VehiclePlate, string.Empty, Arg.Any<CancellationToken>())
            .Returns((Vehicle?)null);
        vehicleRepo.GetByPlateAsync(cutOrder.VehiclePlate, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Vehicle>());
        sessionNoGen.GenerateAsync(Arg.Any<TransactionType>(), Arg.Any<CancellationToken>())
            .Returns("LC26050099");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("PC26059999");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CreateWeighingSessionUseCase(
            regRepo,
            sessionRepo,
            vehicleRepo,
            weighRepo,
            ticketSyncService,
            uow,
            currentUser,
            clock,
            sessionNoGen,
            ticketNoGen);

        var result = await sut.ExecuteAsync(
            new CreateWeighingSessionRequest(new[] { cutOrder.Id }, cutOrder.Id),
            CancellationToken.None);

        await sessionRepo.Received(1).AddAsync(
            Arg.Is<WeighingSession>(x =>
                x.Id == result.SessionId
                && x.Weight1 == 12_500m
                && x.Weight1Time == weight1Time
                && x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                && x.Ttcp10WeightSnapshot == 11_000m),
            Arg.Any<CancellationToken>());

        await weighRepo.Received(1).AddAsync(
            Arg.Is<WeighTicket>(x =>
                x.WeighingSessionId == result.SessionId
                && x.RecordRole == WeighTicketRecordRoles.MasterSession
                && x.Weight1 == 12_500m
                && x.Weight1Time == weight1Time
                && x.Weight1User == "tester"
                && x.Ttcp10WeightSnapshot == 11_000m),
            Arg.Any<CancellationToken>());

        await regRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.WeighingSessionId == result.SessionId
                && x.CarryForwardWeight1 == null
                && x.CarryForwardWeight1Time == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWeighingSession_UsesDeletedCarryForwardFromRegistrationCode_WhenErpCutOrderIdChanges()
    {
        var regRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var now = new DateTime(2026, 5, 21, 9, 0, 0);
        var weight1Time = now.AddMinutes(-40);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var activeCutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = "QN.CL.2605/NEW",
            ErpRegistrationCode = "QN.DKPT.2605/0201",
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            ProcessingStage = ProcessingStage.IN_YARD,
            VehiclePlate = "14H-0032",
            ReceiverName = "Driver A",
            CustomerCode = "C1",
            CustomerName = "Customer 1",
            ProductCode = "XM",
            ProductName = "Xi mang",
            PlannedWeight = 10_000m,
            BagCount = 200,
            CreatedAt = now.AddMinutes(-10),
            TransportMethod = TransportMethod.ROAD
        };
        var deletedCutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = "QN.CL.2605/OLD",
            ErpRegistrationCode = "QN.DKPT.2605/0201",
            CarryForwardWeight1 = 12_500m,
            CarryForwardWeight1Time = weight1Time,
            IsDeleted = true,
            DeletedAt = now.AddMinutes(-5)
        };

        regRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { activeCutOrder });
        regRepo.GetLatestDeletedByRegistrationCodesAsync(
                Arg.Is<IReadOnlyCollection<string>>(x => x.Contains("QN.DKPT.2605/0201")),
                Arg.Any<CancellationToken>())
            .Returns(new[] { deletedCutOrder });
        vehicleRepo.GetByPlateAndMoocAsync(activeCutOrder.VehiclePlate, string.Empty, Arg.Any<CancellationToken>())
            .Returns((Vehicle?)null);
        vehicleRepo.GetByPlateAsync(activeCutOrder.VehiclePlate, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Vehicle>());
        sessionNoGen.GenerateAsync(Arg.Any<TransactionType>(), Arg.Any<CancellationToken>())
            .Returns("LC26050123");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("PC26050123");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CreateWeighingSessionUseCase(
            regRepo,
            sessionRepo,
            vehicleRepo,
            weighRepo,
            ticketSyncService,
            uow,
            currentUser,
            clock,
            sessionNoGen,
            ticketNoGen);

        var result = await sut.ExecuteAsync(
            new CreateWeighingSessionRequest(new[] { activeCutOrder.Id }, activeCutOrder.Id),
            CancellationToken.None);

        await sessionRepo.Received(1).AddAsync(
            Arg.Is<WeighingSession>(x =>
                x.Id == result.SessionId
                && x.Weight1 == 12_500m
                && x.Weight1Time == weight1Time
                && x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight1_SyncsMasterTicketFromSession()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var now = new DateTime(2026, 5, 15, 9, 0, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT1,
            VehiclePlate = "51C-12345",
            TransactionType = TransactionType.OUTBOUND
        };
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            CreatedAt = now.AddMinutes(-10),
            ProductCode = "XM",
            ProductName = "Xi mang",
            CustomerCode = "C1",
            CustomerName = "Customer 1",
            PlannedWeight = 10_000m,
            BagCount = 100,
            TransportMethod = TransportMethod.ROAD
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = registration.Id,
            PlannedWeight = 10_000m
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, string.Empty, Arg.Any<CancellationToken>()).Returns((Vehicle?)null);
        vehicleRepo.GetByPlateAsync(session.VehiclePlate, Arg.Any<CancellationToken>()).Returns(Array.Empty<Vehicle>());
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns((WeighTicket?)null);
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("WT-0001");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CaptureSessionWeight1UseCase(
            sessionRepo,
            regRepo,
            vehicleRepo,
            weighRepo,
            ticketSyncService,
            ticketNoGen,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(session.Id, 12_000m, true, WeightMode.AUTO),
            CancellationToken.None);

        await weighRepo.Received(1).AddAsync(
            Arg.Is<WeighTicket>(x =>
                x.RecordRole == WeighTicketRecordRoles.MasterSession &&
                x.Weight1 == 12_000m &&
                x.Weight1Time == now &&
                x.Weight1User == "tester" &&
                x.Weight1Mode == WeightMode.AUTO &&
                x.Weight1IsStable == true &&
                x.Ttcp10WeightSnapshot == 11_000m &&
                x.IsOverWeight == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_SyncsMasterTicketFromSession()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var toleranceProvider = Substitute.For<IToleranceProvider>();
        var overweightService = new WeighingSessionOverweightService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var now = new DateTime(2026, 5, 15, 10, 0, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 32_000m,
            Weight1Time = now.AddMinutes(-30),
            Ttcp10WeightSnapshot = 27_500m
        };
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ErpCutOrderId = "ERP-001",
            CustomerCode = "C1",
            ProductCode = "P1",
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 22_000m
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-0001");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(session.Id, 10_000m, false, WeightMode.MANUAL),
            CancellationToken.None);

        await weighRepo.Received(1).UpdateAsync(
            Arg.Is<WeighTicket>(x =>
                x.Weight1 == 32_000m &&
                x.Weight1Time == session.Weight1Time &&
                x.Weight2 == 10_000m &&
                x.Weight2Time == now &&
                x.Weight2User == "tester" &&
                x.Weight2Mode == WeightMode.MANUAL &&
                x.Weight2IsStable == false &&
                x.NetWeight == 22_000m &&
                x.Ttcp10WeightSnapshot == 27_500m &&
                x.IsOverWeight == false),
            Arg.Any<CancellationToken>());

        await sessionRepo.Received(1).UpdateLineAsync(
            Arg.Is<WeighingSessionLine>(x =>
                x.Id == line.Id &&
                x.LineStatus == WeighingSessionLineStatus.ALLOCATED &&
                x.ActualAllocatedWeight == 22_000m &&
                x.ActualAllocatedBagCount == 440),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_AutoAllocatesSingleLineAndMovesSessionReadyToComplete()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var toleranceProvider = Substitute.For<IToleranceProvider>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 16, 8, 30, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 31_000m,
            Weight1Time = now.AddMinutes(-20),
            Ttcp10WeightSnapshot = 25_000m
        };
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ErpCutOrderId = "ERP-002",
            CustomerCode = "C2",
            ProductCode = "P2",
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 21_000m,
            Notes = "note"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-0002");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(session.Id, 10_000m, true, WeightMode.AUTO),
            CancellationToken.None);

        Assert.Equal(WeighingSessionStatus.READY_TO_COMPLETE, session.SessionStatus);
        await deliveryRepo.Received(1).AddAsync(
            Arg.Is<DeliveryTicket>(x =>
                x.RecordRole == DeliveryTicketRecordRoles.Normal &&
                x.WeighingSessionLineId == line.Id &&
                x.AllocatedWeight == 21_000m &&
                x.AllocatedBagCount == 420),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_AllowsBaggedWeightWithinTolerance()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var toleranceProvider = Substitute.For<IToleranceProvider>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 19, 8, 0, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 31_000m,
            Ttcp10WeightSnapshot = 30_000m
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 20_000m,
            BagCount = 400,
            ErpCutOrderId = "ERP-BAG-OK",
            CustomerCode = "C1",
            ProductCode = "P1"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-OK");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(session.Id, 10_600m, true, WeightMode.AUTO),
            CancellationToken.None);

        await sessionRepo.Received(1).UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_BlocksBaggedWeightUsingMasterProductTypeFallback()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var toleranceProvider = Substitute.For<IToleranceProvider>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 19, 8, 10, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 2_000m,
            Ttcp10WeightSnapshot = 8_800m
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ProductType = null,
            PlannedWeight = 9_000m,
            BagCount = 180,
            ErpCutOrderId = "ERP-BAG-FALLBACK",
            CustomerCode = "C1",
            ProductCode = "P1"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        productRepo.GetByCodeAsync("P1", Arg.Any<CancellationToken>())
            .Returns(new Product { ProductCode = "P1", ProductType = ProductTypes.Bagged });

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CaptureSessionWeightRequest(session.Id, 11_400m, true, WeightMode.AUTO),
                CancellationToken.None));

        Assert.Contains("vượt dung sai cho phép", ex.Message);
        await sessionRepo.DidNotReceive().UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_BlocksBaggedWeightExceedingTolerance()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var toleranceProvider = Substitute.For<IToleranceProvider>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 19, 8, 5, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 31_000m,
            Ttcp10WeightSnapshot = 30_000m
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 20_000m,
            BagCount = 400,
            ErpCutOrderId = "ERP-BAG-BLOCK",
            CustomerCode = "C1",
            ProductCode = "P1"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CaptureSessionWeightRequest(session.Id, 9_900m, true, WeightMode.AUTO),
                CancellationToken.None));

        Assert.Contains("vượt dung sai cho phép", ex.Message);
        await sessionRepo.DidNotReceive().UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllocateWeighingSession_SyncsOverweightStateToMasterTicket()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 15, 11, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.ALLOCATION_PENDING,
            Weight1 = 45_000m,
            Weight2 = 33_000m,
            NetWeight = 12_000m,
            Ttcp10WeightSnapshot = 10_000m,
            TransactionType = TransactionType.OUTBOUND
        };
        var line = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            CustomerCode = "C1",
            ProductCode = "P1"
        };
        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession,
            Weight1 = session.Weight1,
            Weight1Time = session.Weight1Time,
            Weight2 = session.Weight2,
            Weight2Time = session.Weight2Time
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { masterTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-0001");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new AllocateWeighingSessionUseCase(
            sessionRepo,
            regRepo,
            weighRepo,
            deliveryRepo,
            deliveryNoGen,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new AllocateWeighingSessionRequest(
                session.Id,
                new[]
                {
                    new AllocateWeighingSessionLineRequest(line.Id, 12_000m, 120)
                }),
            CancellationToken.None);

        await weighRepo.Received(1).UpdateAsync(
            Arg.Is<WeighTicket>(x =>
                x.RecordRole == WeighTicketRecordRoles.MasterSession &&
                x.NetWeight == 12_000m &&
                x.Ttcp10WeightSnapshot == 10_000m &&
                x.IsOverWeight == true),
            Arg.Any<CancellationToken>());
    }
}


