using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class NoLoadUseCaseTests
{
    [Fact]
    public async Task MarkRegistrationsNoLoad_CreatesCompletedSessionAndMovesRegistrationsOut()
    {
        var regRepo = Substitute.For<ICutOrderRepository>();
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionNoGen = Substitute.For<IWeighingSessionNumberGenerator>();
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(new DateTime(2026, 5, 14, 9, 0, 0));
        sessionNoGen.GenerateAsync(Arg.Any<TransactionType>(), Arg.Any<CancellationToken>()).Returns("LC26050001");

        var registrations = new[]
        {
            new CutOrder
            {
                Id = Guid.NewGuid(),
                VehiclePlate = "51C-12345",
                TransactionType = TransactionType.OUTBOUND,
                CutOrderStatus = CutOrderStatus.REGISTERED,
                ProcessingStage = ProcessingStage.IN_YARD,
                PlannedWeight = 1000m,
                BagCount = 20,
                CreatedAt = new DateTime(2026, 5, 14, 8, 0, 0)
            }
        };

        regRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(registrations);
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new MarkRegistrationsNoLoadUseCase(regRepo, sessionRepo, uow, currentUser, clock, sessionNoGen);

        var sessionId = await sut.ExecuteAsync(
            new MarkRegistrationsNoLoadRequest(new[] { registrations[0].Id }, registrations[0].Id),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, sessionId);
        await sessionRepo.Received(1).AddAsync(
            Arg.Is<WeighingSession>(x =>
                x.Id == sessionId &&
                x.SessionStatus == WeighingSessionStatus.COMPLETED &&
                x.NetWeight == 0m),
            Arg.Any<CancellationToken>());
        await sessionRepo.Received(1).AddLineAsync(
            Arg.Is<WeighingSessionLine>(x =>
                x.WeighingSessionId == sessionId &&
                x.LineStatus == WeighingSessionLineStatus.ALLOCATED &&
                x.ActualAllocatedWeight == 0m),
            Arg.Any<CancellationToken>());
        await regRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.CutOrderStatus == CutOrderStatus.COMPLETED &&
                x.ProcessingStage == ProcessingStage.OUT_YARD &&
                x.SyncStatus == SyncStatus.SYNC_QUEUED &&
                x.WeighingSessionId == sessionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkWeighingSessionNoLoad_CompletesSessionAndCancelsExistingDocuments()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var regRepo = Substitute.For<ICutOrderRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var ticketSyncService = new WeighingSessionTicketSyncService();
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(new DateTime(2026, 5, 14, 10, 0, 0));

        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE,
            Weight1 = 15000m,
            Weight2 = 15000m
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
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            ProcessingStage = ProcessingStage.WEIGHING
        };
        var weighTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id,
            Status = TicketStatus.LOADING_STARTED,
            RecordRole = "CUT_ORDER_DERIVED"
        };
        var deliveryTicket = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            WeighingSessionId = session.Id
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { line });
        regRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { registration });
        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { weighTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(new[] { deliveryTicket });
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sut = new MarkWeighingSessionNoLoadUseCase(
            sessionRepo,
            regRepo,
            weighRepo,
            deliveryRepo,
            ticketSyncService,
            uow,
            currentUser,
            clock);

        await sut.ExecuteAsync(new MarkWeighingSessionNoLoadRequest(session.Id), CancellationToken.None);

        Assert.Equal(WeighingSessionStatus.COMPLETED, session.SessionStatus);
        Assert.Equal(15000m, session.Weight2);
        Assert.Equal(0m, session.NetWeight);
        await sessionRepo.Received(1).UpdateAsync(Arg.Is<WeighingSession>(x => x.SessionStatus == WeighingSessionStatus.COMPLETED), Arg.Any<CancellationToken>());
        await sessionRepo.Received(1).UpdateLineAsync(Arg.Is<WeighingSessionLine>(x => x.LineStatus == WeighingSessionLineStatus.ALLOCATED && x.ActualAllocatedWeight == 0m), Arg.Any<CancellationToken>());
        await regRepo.Received(1).UpdateAsync(Arg.Is<CutOrder>(x => x.CutOrderStatus == CutOrderStatus.COMPLETED && x.ProcessingStage == ProcessingStage.OUT_YARD && x.SyncStatus == SyncStatus.SYNC_QUEUED), Arg.Any<CancellationToken>());
        await weighRepo.Received(1).UpdateAsync(Arg.Any<WeighTicket>(), Arg.Any<CancellationToken>());
        await deliveryRepo.Received(1).UpdateAsync(Arg.Any<DeliveryTicket>(), Arg.Any<CancellationToken>());

        Assert.True(weighTicket.IsDeleted);
        Assert.True(weighTicket.IsCancelled);
        Assert.Equal(TicketStatus.TICKET_CANCELLED, weighTicket.Status);
        Assert.Equal(SyncStatus.SYNC_QUEUED, weighTicket.SyncStatus);

        Assert.True(deliveryTicket.IsDeleted);
        Assert.Equal(SyncStatus.SYNC_QUEUED, deliveryTicket.SyncStatus);
    }
}


