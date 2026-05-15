using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Sync.Services;
using Xunit;

namespace StationApp.Sync.Tests;

public class SyncOutboxWorkerTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncOutboxWorker> _logger;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IVehicleRegistrationRepository _registrationRepo;
    private readonly IWeighTicketRepository _weighTicketRepo;
    private readonly IDeliveryTicketRepository _deliveryTicketRepo;
    private readonly ISyncPayloadFactory _payloadFactory;
    private readonly IUnitOfWork _uow;
    private readonly ICentralApiClient _apiClient;
    private readonly IClock _clock;

    public SyncOutboxWorkerTests()
    {
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scope = Substitute.For<IServiceScope>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<SyncOutboxWorker>>();
        _outboxRepo = Substitute.For<ISyncOutboxRepository>();
        _registrationRepo = Substitute.For<IVehicleRegistrationRepository>();
        _weighTicketRepo = Substitute.For<IWeighTicketRepository>();
        _deliveryTicketRepo = Substitute.For<IDeliveryTicketRepository>();
        _payloadFactory = Substitute.For<ISyncPayloadFactory>();
        _uow = Substitute.For<IUnitOfWork>();
        _apiClient = Substitute.For<ICentralApiClient>();
        _clock = Substitute.For<IClock>();

        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);

        RegisterService(_outboxRepo);
        RegisterService(_registrationRepo);
        RegisterService(_weighTicketRepo);
        RegisterService(_deliveryTicketRepo);
        RegisterService(_payloadFactory);
        RegisterService(_uow);
        RegisterService(_apiClient);
        RegisterService(_clock);

        _clock.NowLocal.Returns(new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Unspecified));
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        _outboxRepo.GetPendingAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SyncOutbox>());
        _registrationRepo.GetBySyncStatusAsync(Arg.Any<SyncStatus>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<VehicleRegistration>());
        _weighTicketRepo.GetBySyncStatusAsync(Arg.Any<SyncStatus>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WeighTicket>());
        _deliveryTicketRepo.GetBySyncStatusAsync(Arg.Any<SyncStatus>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DeliveryTicket>());
    }

    [Fact]
    public async Task T1_ProcessBatch_RecoversQueuedEntitiesIntoOutbox()
    {
        var registration = new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            SyncStatus = SyncStatus.SYNC_QUEUED
        };
        var weighTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid(),
            SyncStatus = SyncStatus.SYNC_QUEUED
        };
        var deliveryTicket = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            SyncStatus = SyncStatus.SYNC_QUEUED
        };

        _registrationRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, Arg.Any<CancellationToken>())
            .Returns(new[] { registration });
        _weighTicketRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, Arg.Any<CancellationToken>())
            .Returns(new[] { weighTicket });
        _deliveryTicketRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, Arg.Any<CancellationToken>())
            .Returns(new[] { deliveryTicket });

        _payloadFactory.CreatePayload(registration).Returns("{\"kind\":\"registration\"}");
        _payloadFactory.CreatePayload(weighTicket).Returns("{\"kind\":\"ticket\"}");
        _payloadFactory.CreatePayload(deliveryTicket).Returns("{\"kind\":\"delivery\"}");

        _outboxRepo.GetLatestByAggregateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SyncOutbox?)null);

        var worker = new SyncOutboxWorker(_scopeFactory, _logger);

        await RunCycleAsync(worker, CancellationToken.None);

        await _outboxRepo.Received(1).EnqueueAsync(
            Arg.Is<SyncOutbox>(m =>
                m.AggregateId == registration.Id &&
                m.AggregateType == SyncAggregateTypes.VehicleRegistration &&
                m.IdempotencyKey == registration.IdempotencyKey &&
                m.PayloadJson == "{\"kind\":\"registration\"}" &&
                m.Status == OutboxStatus.PENDING),
            Arg.Any<CancellationToken>());

        await _outboxRepo.Received(1).EnqueueAsync(
            Arg.Is<SyncOutbox>(m =>
                m.AggregateId == weighTicket.Id &&
                m.AggregateType == SyncAggregateTypes.WeighTicket &&
                m.IdempotencyKey == weighTicket.IdempotencyKey &&
                m.PayloadJson == "{\"kind\":\"ticket\"}" &&
                m.Status == OutboxStatus.PENDING),
            Arg.Any<CancellationToken>());

        await _outboxRepo.Received(1).EnqueueAsync(
            Arg.Is<SyncOutbox>(m =>
                m.AggregateId == deliveryTicket.Id &&
                m.AggregateType == SyncAggregateTypes.DeliveryTicket &&
                m.IdempotencyKey == deliveryTicket.Id &&
                m.PayloadJson == "{\"kind\":\"delivery\"}" &&
                m.Status == OutboxStatus.PENDING),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task T2_ProcessBatch_PushSuccess_MarksAllAggregateTypesAsSynced()
    {
        var registration = new VehicleRegistration { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var weighTicket = new WeighTicket { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var deliveryTicket = new DeliveryTicket { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var pendingMessages = new[]
        {
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = registration.Id,
                AggregateType = SyncAggregateTypes.VehicleRegistration,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            },
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = weighTicket.Id,
                AggregateType = SyncAggregateTypes.WeighTicket,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            },
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = deliveryTicket.Id,
                AggregateType = SyncAggregateTypes.DeliveryTicket,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            }
        };

        _outboxRepo.GetPendingAsync(_clock.NowLocal, 10, Arg.Any<CancellationToken>()).Returns(pendingMessages);
        _apiClient.PushAggregateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new SyncWeighTicketResponse { Success = true });
        _registrationRepo.GetByIdAsync(registration.Id, Arg.Any<CancellationToken>()).Returns(registration);
        _weighTicketRepo.GetByIdAsync(weighTicket.Id, Arg.Any<CancellationToken>()).Returns(weighTicket);
        _deliveryTicketRepo.GetByIdAsync(deliveryTicket.Id, Arg.Any<CancellationToken>()).Returns(deliveryTicket);

        var worker = new SyncOutboxWorker(_scopeFactory, _logger);

        await RunCycleAsync(worker, CancellationToken.None);

        await _outboxRepo.Received(3).MarkSuccessAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _registrationRepo.Received(1).UpdateAsync(
            Arg.Is<VehicleRegistration>(x =>
                x.SyncStatus == SyncStatus.SYNC_SUCCESS &&
                x.LastSyncAttemptAt == _clock.NowLocal &&
                x.LastSyncError == null),
            Arg.Any<CancellationToken>());
        await _weighTicketRepo.Received(1).UpdateAsync(
            Arg.Is<WeighTicket>(x => x.SyncStatus == SyncStatus.SYNC_SUCCESS),
            Arg.Any<CancellationToken>());
        await _deliveryTicketRepo.Received(1).UpdateAsync(
            Arg.Is<DeliveryTicket>(x => x.SyncStatus == SyncStatus.SYNC_SUCCESS),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task T3_ProcessBatch_PushFailure_MarksAllAggregateTypesAsFailed()
    {
        var registration = new VehicleRegistration { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var weighTicket = new WeighTicket { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var deliveryTicket = new DeliveryTicket { Id = Guid.NewGuid(), SyncStatus = SyncStatus.SYNC_QUEUED };
        var pendingMessages = new[]
        {
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = registration.Id,
                AggregateType = SyncAggregateTypes.VehicleRegistration,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            },
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = weighTicket.Id,
                AggregateType = SyncAggregateTypes.WeighTicket,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            },
            new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = deliveryTicket.Id,
                AggregateType = SyncAggregateTypes.DeliveryTicket,
                IdempotencyKey = Guid.NewGuid(),
                PayloadJson = "{}",
                RetryCount = 0,
                Status = OutboxStatus.PENDING,
                CreatedAt = _clock.NowLocal
            }
        };

        _outboxRepo.GetPendingAsync(_clock.NowLocal, 10, Arg.Any<CancellationToken>()).Returns(pendingMessages);
        _apiClient.PushAggregateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new SyncWeighTicketResponse { Success = false, ErrorMessage = "central api down" });
        _registrationRepo.GetByIdAsync(registration.Id, Arg.Any<CancellationToken>()).Returns(registration);
        _weighTicketRepo.GetByIdAsync(weighTicket.Id, Arg.Any<CancellationToken>()).Returns(weighTicket);
        _deliveryTicketRepo.GetByIdAsync(deliveryTicket.Id, Arg.Any<CancellationToken>()).Returns(deliveryTicket);

        var worker = new SyncOutboxWorker(_scopeFactory, _logger);

        await RunCycleAsync(worker, CancellationToken.None);

        await _outboxRepo.Received(3).MarkFailedRetryableAsync(
            Arg.Any<Guid>(),
            "central api down",
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        await _registrationRepo.Received(1).UpdateAsync(
            Arg.Is<VehicleRegistration>(x =>
                x.SyncStatus == SyncStatus.SYNC_FAILED &&
                x.LastSyncAttemptAt == _clock.NowLocal &&
                x.LastSyncError == "central api down"),
            Arg.Any<CancellationToken>());
        await _weighTicketRepo.Received(1).UpdateAsync(
            Arg.Is<WeighTicket>(x => x.SyncStatus == SyncStatus.SYNC_FAILED),
            Arg.Any<CancellationToken>());
        await _deliveryTicketRepo.Received(1).UpdateAsync(
            Arg.Is<DeliveryTicket>(x => x.SyncStatus == SyncStatus.SYNC_FAILED),
            Arg.Any<CancellationToken>());
    }

    private void RegisterService<TService>(TService instance) where TService : class
    {
        _serviceProvider.GetService(typeof(TService)).Returns(instance);
    }

    private static async Task RunCycleAsync(SyncOutboxWorker worker, CancellationToken ct)
    {
        var method = typeof(SyncOutboxWorker)
            .GetMethod("ProcessBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(worker, new object[] { ct })!;
    }
}
