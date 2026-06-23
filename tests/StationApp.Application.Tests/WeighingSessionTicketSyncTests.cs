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
    private static IWeighingSessionImageRepository CreateImageRepo() =>
        Substitute.For<IWeighingSessionImageRepository>();

    private static ICameraSettingsProvider CreateCameraSettingsProvider()
    {
        var provider = Substitute.For<ICameraSettingsProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(
            new CameraSystemSettings(
                new CameraEndpointSettings("CAM1", "Camera 1", string.Empty, string.Empty, false),
                new CameraEndpointSettings("CAM2", "Camera 2", string.Empty, string.Empty, false),
                "CAM1",
                3000,
                85,
                5));
        return provider;
    }

    private static ICameraCaptureService CreateCameraCaptureService()
    {
        var service = Substitute.For<ICameraCaptureService>();
        service.CaptureAsync(
                Arg.Any<IReadOnlyList<CameraEndpointSettings>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CameraCaptureImageResult>());
        return service;
    }

    [Fact]
    public async Task CreateWeighingSession_RejectsCarryForwardWeight1ForNewSession()
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateWeighingSessionRequest(new[] { cutOrder.Id }, cutOrder.Id, true),
                CancellationToken.None));

        Assert.Equal("Chỉ được dùng lại cân lần 1 khi gắn vào lượt cân cũ phù hợp.", ex.Message);
    }

    [Fact]
    public async Task CreateWeighingSession_CreatesPendingWeight1Session_WhenDeletedCarryForwardExists()
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
        regRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { activeCutOrder });
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
                && x.Weight1 == null
                && x.Weight1Time == null
                && x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendCutOrdersToWeighingSession_RejectsReuse_WhenWeight1IsOlderThan24Hours()
    {
        var regRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 22, 10, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = "LC26052201",
            TransactionType = TransactionType.OUTBOUND,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            VehiclePlate = "14C-12345",
            Weight1 = 12_500m,
            Weight1Time = now.AddHours(-25)
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var sut = new AppendCutOrdersToWeighingSessionUseCase(
            regRepo,
            sessionRepo,
            weighRepo,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new AppendCutOrdersToWeighingSessionRequest(session.Id, new[] { Guid.NewGuid() }),
                CancellationToken.None));

        Assert.Equal("Lượt cân cũ chỉ được phép dùng lại trong vòng 24 giờ kể từ thời điểm cân lần 1.", ex.Message);
    }

    [Fact]
    public async Task AppendCutOrdersToWeighingSession_UpdatesMasterTicketAndSession_WhenReusingReissuedSession()
    {
        var regRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 27, 9, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = "LC26052701",
            TransactionType = TransactionType.OUTBOUND,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            VehiclePlate = "OLD-PLATE",
            MoocNumber = "OLD-MOOC",
            DriverName = "Old Driver",
            Weight1 = 12_500m,
            Weight1Time = now.AddMinutes(-30)
        };
        var orphanLine = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var newCutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = "ERP-NEW-REISSUE",
            ErpRegistrationCode = "DKPT-001",
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = "NEW-PLATE",
            MoocNumber = "NEW-MOOC",
            ReceiverName = "New Driver",
            CustomerCode = "C1",
            CustomerName = "Customer",
            ProductCode = "P1",
            ProductName = "Product",
            PlannedWeight = 20_000m,
            BagCount = 400,
            Notes = "new note",
            ProcessingStage = ProcessingStage.IN_YARD,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            CreatedAt = now
        };
        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = orphanLine.CutOrderId,
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession,
            ErpCutOrderId = "ERP-OLD-REISSUE",
            VehiclePlate = "OLD-PLATE",
            MoocNumber = "OLD-MOOC",
            DriverName = "Old Driver"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { orphanLine });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<CutOrder>());
        regRepo.GetByIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Contains(newCutOrder.Id)), Arg.Any<CancellationToken>())
            .Returns(new[] { newCutOrder });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(masterTicket);
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new AppendCutOrdersToWeighingSessionUseCase(
            regRepo,
            sessionRepo,
            weighRepo,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new AppendCutOrdersToWeighingSessionRequest(session.Id, new[] { newCutOrder.Id }),
            CancellationToken.None);

        Assert.True(orphanLine.IsDeleted);
        Assert.Equal(WeighingSessionLineStatus.CANCELLED, orphanLine.LineStatus);
        Assert.Equal(session.Id, newCutOrder.WeighingSessionId);
        Assert.Equal(CutOrderStatus.IN_SESSION, newCutOrder.CutOrderStatus);
        Assert.Equal(ProcessingStage.WEIGHING, newCutOrder.ProcessingStage);
        Assert.Equal(newCutOrder.Id, masterTicket.CutOrderId);
        Assert.Equal(newCutOrder.ErpCutOrderId, masterTicket.ErpCutOrderId);
        Assert.Equal("NEW-PLATE", masterTicket.VehiclePlate);
        Assert.Equal("NEW-MOOC", masterTicket.MoocNumber);
        Assert.Equal("New Driver", masterTicket.DriverName);
        Assert.Equal("NEW-PLATE", session.VehiclePlate);
        Assert.Equal("NEW-MOOC", session.MoocNumber);
        Assert.Equal("New Driver", session.DriverName);
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
        vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, string.Empty, Arg.Any<CancellationToken>()).Returns(
            new Vehicle
            {
                VehiclePlate = session.VehiclePlate,
                TtcpWeight = 10_000m
            });
        vehicleRepo.GetByPlateAsync(session.VehiclePlate, Arg.Any<CancellationToken>()).Returns(Array.Empty<Vehicle>());
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns((WeighTicket?)null);
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("WT-0001");
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var complianceSettingsProvider = Substitute.For<IIncomingVehicleComplianceSettingsProvider>();
        complianceSettingsProvider.GetCurrentRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new IncomingVehicleComplianceRules(
                new IncomingVehicleComplianceRuleSet(true, false),
                new IncomingVehicleComplianceRuleSet(true, false)));

        var sut = new CaptureSessionWeight1UseCase(
            sessionRepo,
            regRepo,
            vehicleRepo,
            weighRepo,
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
            ticketSyncService,
            ticketNoGen,
            complianceSettingsProvider,
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
            PlannedWeight = 22_000m,
            BagCount = 440
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
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
            BagCount = 420,
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
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
    public async Task CaptureSessionWeight2_ExportScaleBagged_UsesConfirmedBagCount()
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
        var now = new DateTime(2026, 6, 22, 9, 15, 0);
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(now);
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 60_000m,
            Weight1Time = now.AddMinutes(-20)
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
            LineStatus = WeighingSessionLineStatus.PENDING,
            PlannedBagCount = 1_000
        };
        var registration = new CutOrder
        {
            Id = line.CutOrderId,
            ErpCutOrderId = "ERP-EXPORT-001",
            CustomerCode = "C1",
            ProductCode = "P1",
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 50_000m,
            BagWeightKg = 50m,
            BagCount = 1_000,
            IsExportScale = true
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetPrimaryByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-EXPORT-001");
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(
            new CaptureSessionWeightRequest(
                session.Id,
                10_000m,
                true,
                WeightMode.AUTO,
                ConfirmedBagCount: 998,
                SystemCalculatedBagCount: 1_000,
                Note: "  Giao gap cau 2  "),
            CancellationToken.None);

        Assert.Equal(998, line.ActualAllocatedBagCount);
        Assert.Equal(998, line.BagCountDisplay);
        Assert.Equal(1_000, line.SystemCalculatedBagCount);
        Assert.Equal(now, line.BagCountConfirmedAt);
        Assert.Equal("tester", line.BagCountConfirmedBy);
        Assert.Equal("AdjustedManual", line.BagCountConfirmationMode);
        Assert.Equal("Giao gap cau 2", line.Note);
        Assert.Equal(998, ticket.BagCount);

        await deliveryRepo.Received(1).AddAsync(
            Arg.Is<DeliveryTicket>(x =>
                x.AllocatedWeight == 50_000m &&
                x.AllocatedBagCount == 998),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight2_ExportScaleBagged_RequiresConfirmedBagCount()
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
        currentUser.Username.Returns("tester");
        currentUser.RoleCode.Returns("ADMIN");
        clock.NowLocal.Returns(new DateTime(2026, 6, 22, 9, 30, 0));
        toleranceProvider.GetToleranceKgPerBagAsync(Arg.Any<CancellationToken>()).Returns(1.75m);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 60_000m
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
            ProductCode = "P1",
            ProductType = ProductTypes.Bagged,
            PlannedWeight = 50_000m,
            BagWeightKg = 50m,
            BagCount = 1_000,
            IsExportScale = true
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CaptureSessionWeightRequest(session.Id, 10_000m, true, WeightMode.AUTO),
                CancellationToken.None));

        Assert.Contains("số bao", ex.Message, StringComparison.OrdinalIgnoreCase);
        await sessionRepo.DidNotReceive().UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
        await sessionRepo.DidNotReceive().UpdateLineAsync(Arg.Any<WeighingSessionLine>(), Arg.Any<CancellationToken>());
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<BaggedWeightToleranceExceededException>(() =>
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
            CreateImageRepo(),
            CreateCameraSettingsProvider(),
            CreateCameraCaptureService(),
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        var ex = await Assert.ThrowsAsync<BaggedWeightToleranceExceededException>(() =>
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
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
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
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("PC-LINE-0001");
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
            ticketNoGen,
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

    [Fact]
    public async Task AllocateWeighingSession_CreatesPerCutOrderWeighTickets_ForMultiLineSession()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 15, 12, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.ALLOCATION_PENDING,
            VehiclePlate = "14C-18011",
            Weight1 = 1_000m,
            Weight2 = 31_000m,
            NetWeight = 30_000m,
            Ttcp10WeightSnapshot = 33_000m,
            TransactionType = TransactionType.OUTBOUND
        };
        var line1 = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 1,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var line2 = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = Guid.NewGuid(),
            SequenceNo = 2,
            LineStatus = WeighingSessionLineStatus.PENDING
        };
        var registration1 = new CutOrder
        {
            Id = line1.CutOrderId,
            ErpCutOrderId = "QN.CL.2605/1194",
            CustomerCode = "C1",
            CustomerName = "Customer 1",
            ProductCode = "P1",
            ProductName = "Prod 1",
            PlannedWeight = 18_000m,
            BagCount = 360
        };
        var registration2 = new CutOrder
        {
            Id = line2.CutOrderId,
            ErpCutOrderId = "QN.CL.2605/1195",
            CustomerCode = "C2",
            CustomerName = "Customer 2",
            ProductCode = "P2",
            ProductName = "Prod 2",
            PlannedWeight = 12_000m,
            BagCount = 240
        };
        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession,
            TicketNo = "PC26050010",
            VehicleRegistrationNoSnapshot = "DKX001",
            MoocRegistrationNoSnapshot = "DKM001"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line1, line2 });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration1, registration2 });
        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { masterTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DT-0001", "DT-0002");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("PC26050011", "PC26050012");
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
            ticketNoGen,
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
                    new AllocateWeighingSessionLineRequest(line1.Id, 18_000m, 360),
                    new AllocateWeighingSessionLineRequest(line2.Id, 12_000m, 240)
                }),
            CancellationToken.None);

        await weighRepo.Received(2).AddAsync(
            Arg.Is<WeighTicket>(x =>
                x.RecordRole == WeighTicketRecordRoles.CutOrderDerived
                && x.WeighingSessionId == session.Id
                && ((x.CutOrderId == registration1.Id && x.Weight1 == 1_000m && x.Weight2 == 19_000m && x.NetWeight == 18_000m)
                    || (x.CutOrderId == registration2.Id && x.Weight1 == 19_000m && x.Weight2 == 31_000m && x.NetWeight == 12_000m))),
            Arg.Any<CancellationToken>());

        await regRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x => x.Id == registration1.Id && x.CurrentPrimaryWeighTicketId.HasValue),
            Arg.Any<CancellationToken>());
        await regRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x => x.Id == registration2.Id && x.CurrentPrimaryWeighTicketId.HasValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllocateWeighingSession_AllocatesUniqueWeighTicketNumbers_WhenGeneratorWouldRepeatBeforeSave()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var overweightService = new WeighingSessionOverweightService();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var now = new DateTime(2026, 5, 26, 15, 30, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.ALLOCATION_PENDING,
            VehiclePlate = "14C-17921",
            MoocNumber = "14R-01826",
            Weight1 = 3_000m,
            Weight2 = 48_014m,
            NetWeight = 45_014m,
            Ttcp10WeightSnapshot = 22_550m,
            TransactionType = TransactionType.OUTBOUND
        };
        var line1 = new WeighingSessionLine { Id = Guid.NewGuid(), WeighingSessionId = session.Id, CutOrderId = Guid.NewGuid(), SequenceNo = 1, LineStatus = WeighingSessionLineStatus.PENDING };
        var line2 = new WeighingSessionLine { Id = Guid.NewGuid(), WeighingSessionId = session.Id, CutOrderId = Guid.NewGuid(), SequenceNo = 2, LineStatus = WeighingSessionLineStatus.PENDING };
        var registration1 = new CutOrder { Id = line1.CutOrderId, ErpCutOrderId = "QN.CL.2605/1201", CustomerCode = "C1", CustomerName = "Customer 1", ProductCode = "P1", ProductName = "Prod 1", PlannedWeight = 24_000m };
        var registration2 = new CutOrder { Id = line2.CutOrderId, ErpCutOrderId = "QN.CL.2605/1203", CustomerCode = "C2", CustomerName = "Customer 2", ProductCode = "P2", ProductName = "Prod 2", PlannedWeight = 21_000m, BagCount = 420 };
        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            CutOrderId = registration1.Id,
            RecordRole = WeighTicketRecordRoles.MasterSession,
            TicketNo = "PC26050017",
            VehicleRegistrationNoSnapshot = "DKX001",
            MoocRegistrationNoSnapshot = "DKM001"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line1, line2 });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration1, registration2 });
        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { masterTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("PGN26050021");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("PC26050018", "PC26050019");
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
            ticketNoGen,
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
                    new AllocateWeighingSessionLineRequest(line2.Id, 21_000m, 420, true),
                    new AllocateWeighingSessionLineRequest(line1.Id, 24_014m, null)
                }),
            CancellationToken.None);

        await weighRepo.Received(2).AddAsync(
            Arg.Is<WeighTicket>(x =>
                x.RecordRole == WeighTicketRecordRoles.CutOrderDerived
                && ((x.TicketNo == "PC26050018" && x.CutOrderId == registration2.Id && x.Weight1 == 3_000m && x.Weight2 == 24_000m && x.NetWeight == 21_000m)
                    || (x.TicketNo == "PC26050019" && x.CutOrderId == registration1.Id && x.Weight1 == 24_000m && x.Weight2 == 48_014m && x.NetWeight == 24_014m))),
            Arg.Any<CancellationToken>());
    }
}


