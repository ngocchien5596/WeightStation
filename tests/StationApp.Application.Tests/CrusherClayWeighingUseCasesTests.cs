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
            auditLogRepo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateSessionAsync(
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

        Assert.Contains("chưa có trọng lượng xe chuẩn", ex.Message, StringComparison.OrdinalIgnoreCase);
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
            auditLogRepo);

        await sut.CaptureWeight2Async(
            new CaptureCrusherWeight2Request(sessionId, 15555m, true, WeightMode.AUTO),
            CancellationToken.None);

        Assert.Equal(15555m, vehicle.TtcpWeight);
        Assert.Equal(now, vehicle.StandardTareUpdatedAt);
        Assert.Equal("operator", vehicle.StandardTareUpdatedBy);
        await vehicleRepo.Received(1).UpdateAsync(vehicle, Arg.Any<CancellationToken>());
        await syncOutboxRepo.Received(1).EnqueueAsync(Arg.Is<SyncOutbox>(x =>
            x.AggregateId == vehicleId && x.AggregateType == SyncAggregateTypes.Vehicle), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClayCreateSessionAsync_RejectsSingleWeigh_WhenStandardTareIsFromPreviousDay()
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateSessionAsync(
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
            CancellationToken.None));

        Assert.Contains("chưa có trọng lượng xe chuẩn", ex.Message, StringComparison.OrdinalIgnoreCase);
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
}
