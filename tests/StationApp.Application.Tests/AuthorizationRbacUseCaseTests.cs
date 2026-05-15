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
        Assert.Equal("Vai tro khong hop le.", result.ErrorMessage);
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
        Assert.Equal("Phai luon con it nhat 1 tai khoan ADMIN dang hoat dong.", result.ErrorMessage);
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
        Assert.Equal("Phai luon con it nhat 1 tai khoan ADMIN dang hoat dong.", result.ErrorMessage);
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
        Assert.Equal("Phai luon con it nhat 1 tai khoan ADMIN dang hoat dong.", result.ErrorMessage);
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
                new UpdateSystemSettingsRequest("ST", "QN", "DN", "0", "30", "15", "0.0025", true),
                CancellationToken.None));
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
        var regRepo = Substitute.For<IVehicleRegistrationRepository>();
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
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        currentUser.RoleCode.Returns("OPERATOR");

        var sut = new CaptureSessionWeight2UseCase(
            sessionRepo,
            weighRepo,
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
