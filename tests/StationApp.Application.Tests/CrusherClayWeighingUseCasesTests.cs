using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class CrusherClayWeighingUseCasesTests
{
    [Fact]
    public async Task CrusherCreateSessionAsync_RejectsSingleWeigh_WhenStandardTareIsFromPreviousDay()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var vehicleId = Guid.NewGuid();
        vehicleRepo.GetByIdAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(new Vehicle
        {
            Id = vehicleId,
            VehiclePlate = "01.386",
            IsInternalVehicle = true,
            TtcpWeight = 15000m,
            StandardTareUpdatedAt = new DateTime(2026, 6, 23, 8, 0, 0)
        });

        stationScope.GetCurrentStationCodeAsync(Arg.Any<CancellationToken>()).Returns("DAP01");
        operationSettings.GetValueAsync("DAP01", StationOperationSettingKeys.CrusherSingleWeighEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        clock.TodayLocal.Returns(new DateTime(2026, 6, 24));

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        var ex = new InvalidOperationException("chưa có trọng lượng bì");

        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateSessionAsync(
            new CreateCrusherSessionRequest(
                vehicleId,
                CrusherWeighingModes.SingleWithStandardTare,
                30000m,
                true,
                WeightMode.AUTO,
                null,
                null,
                null,
                null),
            CancellationToken.None));

        Assert.Contains("chưa có trọng lượng bì", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrusherCaptureWeight2Async_UpdatesVehicleStandardTareFromWeight2()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var vehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 24, 8, 15, 0);
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            VehiclePlate = "01.386",
            IsInternalVehicle = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            VehiclePlate = "01.386",
            WeighingMode = CrusherWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            Weight1 = 30000m,
            StandardTareVehicleId = vehicleId
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        vehicleRepo.GetByIdAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(vehicle);
        payloadFactory.CreatePayload(vehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        await sut.CaptureWeight2Async(
            new CaptureCrusherWeight2Request(sessionId, 15555m, true, WeightMode.AUTO),
            CancellationToken.None);

        Assert.Equal(15555m, vehicle.TtcpWeight);
        Assert.Equal(now, vehicle.StandardTareUpdatedAt);
        Assert.Equal("operator", vehicle.StandardTareUpdatedBy);
        Assert.Equal(15555m, session.StandardTareWeightSnapshot);
        Assert.Equal(vehicleId, session.StandardTareVehicleId);
        await vehicleRepo.Received(1).UpdateAsync(vehicle, Arg.Any<CancellationToken>());
        await syncOutboxRepo.Received(1).EnqueueAsync(Arg.Is<SyncOutbox>(x =>
            x.AggregateId == vehicleId && x.AggregateType == SyncAggregateTypes.Vehicle), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayCreateSessionAsync_ForcesTwoWeigh_WhenSingleWeighIsRequested()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var vehicleId = Guid.NewGuid();
        vehicleRepo.GetByIdAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(new Vehicle
        {
            Id = vehicleId,
            VehiclePlate = "01.386",
            IsInternalVehicle = true,
            TtcpWeight = 15000m,
            StandardTareUpdatedAt = new DateTime(2026, 6, 23, 8, 0, 0)
        });

        stationScope.GetCurrentStationCodeAsync(Arg.Any<CancellationToken>()).Returns("SET01");
        operationSettings.GetValueAsync("SET01", ClayStationOperationSettingKeys.ClaySingleWeighEnabled, Arg.Any<CancellationToken>())
            .Returns("true");
        clock.TodayLocal.Returns(new DateTime(2026, 6, 24));
        clock.NowLocal.Returns(new DateTime(2026, 6, 24, 8, 30, 0));
        sessionNoGen.GenerateAsync(TransactionType.INBOUND, Arg.Any<CancellationToken>()).Returns("LC26060001");
        WeighingSession? addedSession = null;
        sessionRepo.AddAsync(Arg.Do<WeighingSession>(x => addedSession = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        var ex = new InvalidOperationException("chưa có trọng lượng bì");

        await sut.CreateSessionAsync(
            new CreateClaySessionRequest(
                vehicleId,
                ClayWeighingModes.SingleWithStandardTare,
                30000m,
                true,
                WeightMode.AUTO,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.NotNull(addedSession);
        Assert.Equal(ClayWeighingModes.TwoWeigh, addedSession!.WeighingMode);
        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, addedSession.SessionStatus);
        Assert.Null(addedSession.Weight2);
        Assert.Null(addedSession.Weight2Time);
        Assert.Null(addedSession.NetWeight);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, addedSession.NetWeightCalculationMode);

        Assert.Contains("chưa có trọng lượng bì", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClayCaptureWeight2Async_UpdatesVehicleStandardTareFromWeight2()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var vehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 24, 8, 15, 0);
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            VehiclePlate = "01.386",
            IsInternalVehicle = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            VehiclePlate = "01.386",
            WeighingMode = ClayWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            Weight1 = 30000m,
            StandardTareVehicleId = vehicleId
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        vehicleRepo.GetByIdAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(vehicle);
        payloadFactory.CreatePayload(vehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.CaptureWeight2Async(
            new CaptureClayWeight2Request(sessionId, 15555m, true, WeightMode.AUTO),
            CancellationToken.None);

        Assert.Equal(15555m, vehicle.TtcpWeight);
        Assert.Equal(now, vehicle.StandardTareUpdatedAt);
        Assert.Equal("operator", vehicle.StandardTareUpdatedBy);
        await vehicleRepo.Received(1).UpdateAsync(vehicle, Arg.Any<CancellationToken>());
        await syncOutboxRepo.Received(1).EnqueueAsync(Arg.Is<SyncOutbox>(x =>
            x.AggregateId == vehicleId && x.AggregateType == SyncAggregateTypes.Vehicle), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrusherUpdateSessionVehicleAsync_InvalidatesOldVehicleStandardTare_AndAppliesWeight2_WhenNewVehicleHasNoEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 24, 10, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "B",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 15_555m,
            StandardTareUpdatedAt = now.AddMinutes(-10),
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "A",
            IsInternalVehicle = true,
            IsActive = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            VehiclePlate = "B",
            InternalVehicleNo = "B",
            WeighingMode = CrusherWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 31_000m,
            Weight2 = 15_555m,
            NetWeight = 15_445m,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 15_555m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(1);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(oldVehicle).Returns("{}");
        payloadFactory.CreatePayload(newVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("DAP01");

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Null(oldVehicle.TtcpWeight);
        Assert.Null(oldVehicle.StandardTareUpdatedAt);
        Assert.Equal("operator", oldVehicle.StandardTareUpdatedBy);
        Assert.Equal("A", session.VehiclePlate);
        Assert.Equal(15_555m, session.Weight2);
        Assert.Equal(15_445m, session.NetWeight);
        Assert.Equal(15_555m, session.StandardTareWeightSnapshot);
        Assert.Equal(WeighingSessionStatus.COMPLETED, session.SessionStatus);
        Assert.Equal(NetWeightCalculationModes.Weight1MinusStandardTare, session.NetWeightCalculationMode);
        Assert.Equal(15_555m, newVehicle.TtcpWeight);
        Assert.Equal(now, newVehicle.StandardTareUpdatedAt);
        await vehicleRepo.Received(1).UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.Received(1).UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await syncOutboxRepo.Received(1).EnqueueAsync(Arg.Is<SyncOutbox>(x =>
            x.AggregateId == oldVehicleId && x.AggregateType == SyncAggregateTypes.Vehicle), Arg.Any<CancellationToken>());
        await syncOutboxRepo.Received(1).EnqueueAsync(Arg.Is<SyncOutbox>(x =>
            x.AggregateId == newVehicleId && x.AggregateType == SyncAggregateTypes.Vehicle), Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("InvalidatedOldVehicleStandardTare")
            && x.DetailJson.Contains("\"VehiclePlate\":\"B\"")
            && x.DetailJson.Contains("AppliedStandardTareToNewVehicle")
            && x.DetailJson.Contains("\"VehiclePlate\":\"A\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrusherUpdateSessionVehicleAsync_UsesNewVehicleStandardTare_WhenCompletedTwoWeighSessionChangesVehicle()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 3, 10, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "333",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 14_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "210",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 20_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070015",
            VehiclePlate = "333",
            InternalVehicleNo = "333",
            WeighingMode = CrusherWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 54_000m,
            Weight2 = 14_000m,
            NetWeight = 40_000m,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 14_000m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(1);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(oldVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("DAP01");

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Equal("210", session.VehiclePlate);
        Assert.Equal(20_000m, session.Weight2);
        Assert.Equal(34_000m, session.NetWeight);
        Assert.Equal(20_000m, session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(NetWeightCalculationModes.Weight1MinusStandardTare, session.NetWeightCalculationMode);
        Assert.Null(oldVehicle.TtcpWeight);
        Assert.Null(oldVehicle.StandardTareUpdatedAt);
        await vehicleRepo.Received(1).UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("InvalidatedOldVehicleStandardTare")
            && x.DetailJson.Contains("\"VehiclePlate\":\"333\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrusherUpdateSessionVehicleAsync_AppliesExistingWeight2_WhenCompletedSingleSessionChangesToVehicleWithoutEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 4, 10, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "08",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 20_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "04",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 30_000m,
            StandardTareUpdatedAt = now.AddDays(-1),
            StandardTareUpdatedBy = "operator"
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070018",
            VehiclePlate = "08",
            InternalVehicleNo = "08",
            WeighingMode = CrusherWeighingModes.SingleWithStandardTare,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 58_000m,
            Weight2 = 20_000m,
            NetWeight = 38_000m,
            NetWeightCalculationMode = NetWeightCalculationModes.Weight1MinusStandardTare,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 20_000m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(1);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(oldVehicle).Returns("{}");
        payloadFactory.CreatePayload(newVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("DAP01");

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Equal("04", session.VehiclePlate);
        Assert.Equal(58_000m, session.Weight1);
        Assert.Equal(20_000m, session.Weight2);
        Assert.Equal(38_000m, session.NetWeight);
        Assert.Equal(20_000m, session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(WeighingSessionStatus.COMPLETED, session.SessionStatus);
        Assert.Equal(CrusherWeighingModes.SingleWithStandardTare, session.WeighingMode);
        Assert.Equal(NetWeightCalculationModes.Weight1MinusStandardTare, session.NetWeightCalculationMode);
        Assert.Null(oldVehicle.TtcpWeight);
        Assert.Null(oldVehicle.StandardTareUpdatedAt);
        Assert.Equal(20_000m, newVehicle.TtcpWeight);
        Assert.Equal(now, newVehicle.StandardTareUpdatedAt);
        await vehicleRepo.Received(1).UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.Received(1).UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("InvalidatedOldVehicleStandardTare")
            && x.DetailJson.Contains("\"VehiclePlate\":\"08\"")
            && x.DetailJson.Contains("AppliedStandardTareToNewVehicle")
            && x.DetailJson.Contains("\"VehiclePlate\":\"04\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrusherUpdateSessionVehicleAsync_DoesNotInvalidateOldVehicleStandardTare_WhenOldVehicleHasMultipleCompletedTareSessions()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 3, 10, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "B",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 15_555m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "A",
            IsInternalVehicle = true,
            IsActive = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            VehiclePlate = "B",
            InternalVehicleNo = "B",
            WeighingMode = CrusherWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 31_000m,
            Weight2 = 15_555m,
            NetWeight = 15_445m,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 15_555m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(2);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(newVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("DAP01");

        var sut = new CrusherWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo,
            Substitute.For<ITelegramNotificationService>());

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Equal(15_555m, oldVehicle.TtcpWeight);
        Assert.Equal(now, oldVehicle.StandardTareUpdatedAt);
        Assert.Equal(15_555m, newVehicle.TtcpWeight);
        await vehicleRepo.DidNotReceive().UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.Received(1).UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("\"InvalidatedOldVehicleStandardTare\":null")
            && x.DetailJson.Contains("AppliedStandardTareToNewVehicle")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayUpdateSessionVehicleAsync_CompletesPendingWeight2_WhenNewVehicleHasEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 5, 8, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "OLD",
            IsInternalVehicle = true,
            IsActive = true
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "NEW",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 15_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070021",
            VehiclePlate = "OLD",
            InternalVehicleNo = "OLD",
            WeighingMode = ClayWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            Weight1 = 40_000m,
            Weight2 = null,
            NetWeight = null,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = null
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN01");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "wrong vehicle", CancellationToken.None);

        Assert.Equal("NEW", session.VehiclePlate);
        Assert.Equal(ClayWeighingModes.TwoWeigh, session.WeighingMode);
        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, session.SessionStatus);
        Assert.Null(session.Weight2);
        Assert.Null(session.Weight2Time);
        Assert.Null(session.NetWeight);
        Assert.Equal(15_000m, session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, session.NetWeightCalculationMode);
        await vehicleRepo.DidNotReceive().UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.DidNotReceive().UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("\"VehiclePlate\"")
            && x.DetailJson.Contains("\"Old\":\"OLD\"")
            && x.DetailJson.Contains("\"New\":\"NEW\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayUpdateSessionVehicleAsync_KeepsPendingWeight2_WhenNewVehicleHasNoEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 5, 8, 30, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "OLD",
            IsInternalVehicle = true,
            IsActive = true
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "NEW",
            IsInternalVehicle = true,
            IsActive = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070022",
            VehiclePlate = "OLD",
            InternalVehicleNo = "OLD",
            WeighingMode = ClayWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            Weight1 = 40_000m,
            Weight2 = null,
            NetWeight = null,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = null
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN01");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "wrong vehicle", CancellationToken.None);

        Assert.Equal("NEW", session.VehiclePlate);
        Assert.Equal(ClayWeighingModes.TwoWeigh, session.WeighingMode);
        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, session.SessionStatus);
        Assert.Null(session.Weight2);
        Assert.Null(session.Weight2Time);
        Assert.Null(session.NetWeight);
        Assert.Null(session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, session.NetWeightCalculationMode);
        await vehicleRepo.DidNotReceive().UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.DidNotReceive().UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayUpdateSessionVehicleAsync_InvalidatesOldVehicleStandardTare_AndAppliesWeight2_WhenNewVehicleHasNoEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 5, 10, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "B",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 15_555m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "A",
            IsInternalVehicle = true,
            IsActive = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            VehiclePlate = "B",
            InternalVehicleNo = "B",
            WeighingMode = ClayWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 31_000m,
            Weight2 = 15_555m,
            NetWeight = 15_445m,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 15_555m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(1);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(oldVehicle).Returns("{}");
        payloadFactory.CreatePayload(newVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN01");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Null(oldVehicle.TtcpWeight);
        Assert.Null(oldVehicle.StandardTareUpdatedAt);
        Assert.Equal("A", session.VehiclePlate);
        Assert.Equal(15_555m, session.Weight2);
        Assert.Equal(15_445m, session.NetWeight);
        Assert.Equal(15_555m, session.StandardTareWeightSnapshot);
        Assert.Equal(WeighingSessionStatus.COMPLETED, session.SessionStatus);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, session.NetWeightCalculationMode);
        Assert.Equal(15_555m, newVehicle.TtcpWeight);
        Assert.Equal(now, newVehicle.StandardTareUpdatedAt);
        await vehicleRepo.Received(1).UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.Received(1).UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("InvalidatedOldVehicleStandardTare")
            && x.DetailJson.Contains("\"VehiclePlate\":\"B\"")
            && x.DetailJson.Contains("AppliedStandardTareToNewVehicle")
            && x.DetailJson.Contains("\"VehiclePlate\":\"A\"")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayUpdateSessionVehicleAsync_UsesNewVehicleStandardTare_WhenCompletedSessionChangesVehicle()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 5, 11, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "333",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 14_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "210",
            IsInternalVehicle = true,
            IsActive = true,
            TtcpWeight = 20_000m,
            StandardTareUpdatedAt = now,
            StandardTareUpdatedBy = "operator"
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070015",
            VehiclePlate = "333",
            InternalVehicleNo = "333",
            WeighingMode = ClayWeighingModes.TwoWeigh,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 54_000m,
            Weight2 = 14_000m,
            NetWeight = 40_000m,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = 14_000m
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.CountCompletedStandardTareSessionsForVehicleOnDateAsync(oldVehicleId, now.Date, Arg.Any<CancellationToken>())
            .Returns(1);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        payloadFactory.CreatePayload(oldVehicle).Returns("{}");
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN01");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "Chọn nhầm xe", CancellationToken.None);

        Assert.Equal("210", session.VehiclePlate);
        Assert.Equal(14_000m, session.Weight2);
        Assert.Equal(40_000m, session.NetWeight);
        Assert.Equal(20_000m, session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, session.NetWeightCalculationMode);
        Assert.Null(oldVehicle.TtcpWeight);
        Assert.Null(oldVehicle.StandardTareUpdatedAt);
        await vehicleRepo.Received(1).UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.DidNotReceive().UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.DetailJson != null
            && x.DetailJson.Contains("InvalidatedOldVehicleStandardTare")
            && x.DetailJson.Contains("\"VehiclePlate\":\"333\"")
            && x.DetailJson.Contains("\"AppliedStandardTareToNewVehicle\":null")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayUpdateSessionVehicleAsync_ReopensPendingWeight2_WhenCompletedSessionHasNoWeight2_AndNewVehicleHasNoEffectiveTare()
    {
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var productRepo = Substitute.For<IProductRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        var stationScope = Substitute.For<IStationScope>();
        var operationSettings = Substitute.For<IStationOperationSettingsRepository>();
        var syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
        var payloadFactory = Substitute.For<ISyncPayloadFactory>();
        var clock = Substitute.For<IClock>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();

        var oldVehicleId = Guid.NewGuid();
        var newVehicleId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 5, 12, 0, 0);
        var oldVehicle = new Vehicle
        {
            Id = oldVehicleId,
            VehiclePlate = "OLD",
            IsInternalVehicle = true,
            IsActive = true
        };
        var newVehicle = new Vehicle
        {
            Id = newVehicleId,
            VehiclePlate = "NEW",
            IsInternalVehicle = true,
            IsActive = true
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "LC26070023",
            VehiclePlate = "OLD",
            InternalVehicleNo = "OLD",
            WeighingMode = ClayWeighingModes.SingleWithStandardTare,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            Weight1 = 40_000m,
            Weight2 = null,
            NetWeight = null,
            NetWeightCalculationMode = NetWeightCalculationModes.Weight1MinusStandardTare,
            StandardTareVehicleId = oldVehicleId,
            StandardTareWeightSnapshot = null
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        vehicleRepo.GetByIdAsync(oldVehicleId, Arg.Any<CancellationToken>()).Returns(oldVehicle);
        vehicleRepo.GetByIdAsync(newVehicleId, Arg.Any<CancellationToken>()).Returns(newVehicle);
        clock.NowLocal.Returns(now);
        clock.TodayLocal.Returns(now.Date);
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN01");

        var sut = new ClayWeighingUseCases(
            vehicleRepo,
            customerRepo,
            productRepo,
            sessionRepo,
            sessionNoGen,
            stationScope,
            operationSettings,
            syncOutboxRepo,
            payloadFactory,
            clock,
            currentUser,
            unitOfWork,
            auditLogRepo);

        await sut.UpdateSessionVehicleAsync(sessionId, newVehicleId, "wrong vehicle", CancellationToken.None);

        Assert.Equal("NEW", session.VehiclePlate);
        Assert.Equal(ClayWeighingModes.TwoWeigh, session.WeighingMode);
        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, session.SessionStatus);
        Assert.Null(session.Weight2);
        Assert.Null(session.Weight2Time);
        Assert.Null(session.NetWeight);
        Assert.Null(session.StandardTareWeightSnapshot);
        Assert.Equal(newVehicleId, session.StandardTareVehicleId);
        Assert.Equal(NetWeightCalculationModes.Weight2Diff, session.NetWeightCalculationMode);
        await vehicleRepo.DidNotReceive().UpdateAsync(newVehicle, Arg.Any<CancellationToken>());
        await vehicleRepo.DidNotReceive().UpdateAsync(oldVehicle, Arg.Any<CancellationToken>());
        await sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleCrusherReturnedBrokenTrip_SetsSessionFlag_AndQueuesSync()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 4, 11, 0, 0);
        var session = new WeighingSession
        {
            Id = sessionId,
            InternalVehicleNo = "08",
            SessionStatus = WeighingSessionStatus.COMPLETED,
            NetWeight = 38_000m,
            IsReturnedBrokenTrip = false,
            LastSyncAttemptAt = now.AddMinutes(-10),
            LastSyncError = "old error"
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(0)(CancellationToken.None));
        currentUser.Username.Returns("operator");
        currentUser.StationCode.Returns("QN02");
        clock.NowLocal.Returns(now);

        var sut = new ToggleCrusherReturnedBrokenTripUseCase(sessionRepo, auditLogRepo, unitOfWork, currentUser, clock);

        await sut.ExecuteAsync(sessionId, true, CancellationToken.None);

        Assert.True(session.IsReturnedBrokenTrip);
        Assert.Equal(SyncStatus.SYNC_QUEUED, session.SyncStatus);
        Assert.Null(session.LastSyncAttemptAt);
        Assert.Null(session.LastSyncError);
        Assert.Equal(now, session.UpdatedAt);
        Assert.Equal("operator", session.UpdatedBy);
        await sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
        await auditLogRepo.Received(1).AddAsync(Arg.Is<AuditLog>(x =>
            x.Action == "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP"
            && x.EntityType == nameof(WeighingSession)
            && x.EntityId == sessionId
            && x.Actor == "operator"
            && x.StationCode == "QN02"
            && x.DetailJson != null
            && x.DetailJson.Contains("\"NewIsReturnedBrokenTrip\":true")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleCrusherReturnedBrokenTrip_RejectsPendingSession()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionId = Guid.NewGuid();
        var session = new WeighingSession
        {
            Id = sessionId,
            InternalVehicleNo = "08",
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
            NetWeight = null
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        var sut = new ToggleCrusherReturnedBrokenTripUseCase(sessionRepo, auditLogRepo, unitOfWork, currentUser, clock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(sessionId, true, CancellationToken.None));

        Assert.Contains("đã hoàn thành", ex.Message);
        await sessionRepo.DidNotReceive().UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
        await auditLogRepo.DidNotReceive().AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleCrusherReturnedBrokenTrip_ReturnsWithoutUpdate_WhenStateUnchanged()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var auditLogRepo = Substitute.For<IAuditLogRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionId = Guid.NewGuid();
        var session = new WeighingSession
        {
            Id = sessionId,
            InternalVehicleNo = "08",
            SessionStatus = WeighingSessionStatus.COMPLETED,
            NetWeight = 38_000m,
            IsReturnedBrokenTrip = true
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        var sut = new ToggleCrusherReturnedBrokenTripUseCase(sessionRepo, auditLogRepo, unitOfWork, currentUser, clock);

        await sut.ExecuteAsync(sessionId, true, CancellationToken.None);

        await sessionRepo.DidNotReceive().UpdateAsync(Arg.Any<WeighingSession>(), Arg.Any<CancellationToken>());
        await auditLogRepo.DidNotReceive().AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }
}
