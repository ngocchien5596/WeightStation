using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class AuthorizationRbacUseCaseTests
{
    [Fact]
    public async Task SearchUsers_Operator_IsBlocked()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("OPERATOR");
        var sut = new SearchUsersUseCase(userRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ExecuteAsync(new SearchUsersRequest(null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task CreateUserAccount_Operator_IsBlocked()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IUserPasswordHasher>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var audit = Substitute.For<IAuditService>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new CreateUserAccountUseCase(userRepo, hasher, currentUser, clock, uow, audit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ExecuteAsync(new CreateUserAccountRequest("op", "Operator", "OPERATOR", "password1", "password1", true), CancellationToken.None));
    }

    [Fact]
    public async Task CreateUserAccount_RejectsUnsupportedRoleCode()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IUserPasswordHasher>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var audit = Substitute.For<IAuditService>();
        currentUser.RoleCode.Returns("ADMIN");

        var sut = new CreateUserAccountUseCase(userRepo, hasher, currentUser, clock, uow, audit);
        var result = await sut.ExecuteAsync(
            new CreateUserAccountRequest("user1", "User 1", "SUPERVISOR", "password1", "password1", true),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Vai trò không hợp lệ.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateUserAccount_CannotDeactivateLastActiveAdmin()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var audit = Substitute.For<IAuditService>();
        currentUser.RoleCode.Returns("ADMIN");

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin",
            RoleCode = "ADMIN",
            IsActive = true
        };

        userRepo.GetByIdAsync(adminUser.Id, Arg.Any<CancellationToken>()).Returns(adminUser);
        userRepo.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = new UpdateUserAccountUseCase(userRepo, currentUser, clock, uow, audit);
        var result = await sut.ExecuteAsync(
            new UpdateUserAccountRequest(adminUser.Id, "Admin Updated", "ADMIN", false),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Phải luôn còn ít nhất 1 tài khoản ADMIN đang hoạt động.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateUserAccount_CannotDemoteLastActiveAdmin()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var audit = Substitute.For<IAuditService>();
        currentUser.RoleCode.Returns("ADMIN");

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin",
            RoleCode = "ADMIN",
            IsActive = true
        };

        userRepo.GetByIdAsync(adminUser.Id, Arg.Any<CancellationToken>()).Returns(adminUser);
        userRepo.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = new UpdateUserAccountUseCase(userRepo, currentUser, clock, uow, audit);
        var result = await sut.ExecuteAsync(
            new UpdateUserAccountRequest(adminUser.Id, "Admin Updated", "OPERATOR", true),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Phải luôn còn ít nhất 1 tài khoản ADMIN đang hoạt động.", result.ErrorMessage);
    }

    [Fact]
    public async Task SetUserActiveStatus_CannotDisableLastActiveAdmin()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var audit = Substitute.For<IAuditService>();
        currentUser.RoleCode.Returns("ADMIN");

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            DisplayName = "Admin",
            RoleCode = "ADMIN",
            IsActive = true
        };

        userRepo.GetByIdAsync(adminUser.Id, Arg.Any<CancellationToken>()).Returns(adminUser);
        userRepo.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sut = new SetUserActiveStatusUseCase(userRepo, currentUser, clock, uow, audit);
        var result = await sut.ExecuteAsync(new SetUserActiveStatusRequest(adminUser.Id, false), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Phải luôn còn ít nhất 1 tài khoản ADMIN đang hoạt động.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateSystemSettings_Operator_IsBlocked()
    {
        var configRepo = Substitute.For<IAppConfigRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new UpdateSystemSettingsUseCase(configRepo, uow, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ExecuteAsync(
                new UpdateSystemSettingsRequest("ST", "QN", "DN", "0", "30", "15", "0.0025", "", "", "", "01:00"),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCameraSettings_Operator_IsBlocked()
    {
        var configRepo = Substitute.For<IAppConfigRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new UpdateCameraSettingsUseCase(configRepo, uow, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ExecuteAsync(
                new UpdateCameraSettingsRequest(false, "Camera 1", "", "", false, "Camera 2", "", "", false, "Camera C6-1", "", "", false, "Camera C6-2", "", "", "CAM1", "3000", "85", "5"),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCameraSettings_Admin_SavesAllParameters()
    {
        var configRepo = Substitute.For<IAppConfigRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("ADMIN");

        var sut = new UpdateCameraSettingsUseCase(configRepo, uow, currentUser);

        await sut.ExecuteAsync(
            new UpdateCameraSettingsRequest(
                true, "Camera C2-1", "rtsp://c2-1/live", "rtsp://c2-1/prev",
                true, "Camera C2-2", "rtsp://c2-2/live", "rtsp://c2-2/prev",
                true, "Camera C6-1", "rtsp://c6-1/live", "rtsp://c6-1/prev",
                false, "Camera C6-2", "rtsp://c6-2/live", "rtsp://c6-2/prev",
                "CAM2", "4000", "90", "10"),
            CancellationToken.None);

        await configRepo.Received().SetValueAsync("camera_1_enabled", "true", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_1_name", "Camera C2-1", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_1_rtsp_url", "rtsp://c2-1/live", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_1_preview_rtsp_url", "rtsp://c2-1/prev", Arg.Any<CancellationToken>());

        await configRepo.Received().SetValueAsync("camera_c6_1_enabled", "true", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_c6_1_name", "Camera C6-1", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_c6_1_rtsp_url", "rtsp://c6-1/live", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_c6_1_preview_rtsp_url", "rtsp://c6-1/prev", Arg.Any<CancellationToken>());

        await configRepo.Received().SetValueAsync("camera_preview_default", "CAM2", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_capture_timeout_ms", "4000", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_capture_jpeg_quality", "90", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("camera_capture_warmup_frames", "10", Arg.Any<CancellationToken>());

        await uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateScaleDeviceSettings_Operator_IsBlocked()
    {
        var configRepo = Substitute.For<IAppConfigRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new UpdateScaleDeviceSettingsUseCase(configRepo, uow, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.ExecuteAsync(
                new UpdateScaleDeviceSettingsRequest("COM1", "9600", "None", "8", "One", "DEFAULT", "CR", "3", "", ""),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateScaleDeviceSettings_Admin_SavesAllSerialParameters()
    {
        var configRepo = Substitute.For<IAppConfigRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.RoleCode.Returns("ADMIN");

        var sut = new UpdateScaleDeviceSettingsUseCase(configRepo, uow, currentUser);

        await sut.ExecuteAsync(
            new UpdateScaleDeviceSettingsRequest("COM6", "9600", "Even", "8", "One", "YAOHUA", "CR", "3", "", ""),
            CancellationToken.None);

        await configRepo.Received().SetValueAsync("device_com_port", "COM6", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_baudrate", "9600", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_parity", "Even", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_data_bits", "8", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_stop_bits", "One", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_parser_type", "YAOHUA", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_frame_end_char", "CR", Arg.Any<CancellationToken>());
        await configRepo.Received().SetValueAsync("device_stable_cycles", "3", Arg.Any<CancellationToken>());
        await uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureSessionWeight1_Operator_CannotUseManualMode()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var vehicleRepo = Substitute.For<IVehicleRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new CaptureSessionWeight1UseCase(
            sessionRepo,
            regRepo,
            vehicleRepo,
            weighRepo,
            Substitute.For<IWeighingSessionImageRepository>(),
            Substitute.For<ICameraSettingsProvider>(),
            Substitute.For<ICameraCaptureService>(),
            ticketSyncService,
            ticketNoGen,
            uow,
            currentUser,
            clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CaptureSessionWeightRequest(Guid.NewGuid(), 10000m, true, WeightMode.MANUAL),
                CancellationToken.None));
    }

    [Fact]
    public async Task CaptureSessionWeight2_Operator_CannotUseManualMode()
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
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            regRepo,
            productRepo,
            weighRepo,
            deliveryRepo,
            Substitute.For<IWeighingSessionImageRepository>(),
            Substitute.For<ICameraSettingsProvider>(),
            Substitute.For<ICameraCaptureService>(),
            deliveryNoGen,
            toleranceProvider,
            overweightService,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CaptureSessionWeightRequest(Guid.NewGuid(), 32000m, true, WeightMode.MANUAL),
                CancellationToken.None));
    }
}

