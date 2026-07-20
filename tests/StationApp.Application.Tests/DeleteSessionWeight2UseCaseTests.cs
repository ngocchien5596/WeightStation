using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class DeleteSessionWeight2UseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ResetSessionTicketLineAndDelivery_WhenOutboundSessionHasWeight2()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var audit = Substitute.For<IAuditService>();
        var now = new DateTime(2026, 7, 18, 9, 0, 0);
        currentUser.Username.Returns("manager");
        currentUser.RoleCode.Returns("MANAGER");
        currentUser.StationCode.Returns("QN01");
        clock.NowLocal.Returns(now);
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var sessionId = Guid.NewGuid();
        var cutOrderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var session = new WeighingSession
        {
            Id = sessionId,
            StationCode = "QN01",
            SessionNo = "LC26070099",
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = "14C-12345",
            Weight1 = 54_000m,
            Weight1Time = now.AddMinutes(-30),
            Weight2 = 14_000m,
            Weight2Time = now.AddMinutes(-5),
            NetWeight = 40_000m,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            IsOverweight = true,
            OverweightAmount = 1_000m,
            OverweightResolutionStatus = OverweightResolutionStatus.NO_SPLIT_CONFIRMED,
            LastSyncAttemptAt = now.AddMinutes(-1),
            LastSyncError = "old error"
        };
        var cutOrder = new CutOrder
        {
            Id = cutOrderId,
            StationCode = "QN01",
            ErpCutOrderId = "CL-001",
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.COMPLETED,
            ProcessingStage = ProcessingStage.OUT_YARD,
            WeighingSessionId = sessionId,
            CurrentPrimaryDeliveryTicketId = deliveryId
        };
        var line = new WeighingSessionLine
        {
            Id = lineId,
            WeighingSessionId = sessionId,
            CutOrderId = cutOrderId,
            ActualAllocatedWeight = 40_000m,
            ActualAllocatedBagCount = 800,
            BagCountDisplay = 800,
            SystemCalculatedBagCount = 799,
            BagCountConfirmedAt = now.AddMinutes(-4),
            BagCountConfirmedBy = "operator",
            BagCountConfirmationMode = "AdjustedManual",
            IsReturnedBrokenTrip = true,
            Note = "note",
            LineStatus = WeighingSessionLineStatus.ALLOCATED,
            DeliveryTicketId = deliveryId,
            LastSyncAttemptAt = now.AddMinutes(-1),
            LastSyncError = "line error"
        };
        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = cutOrderId,
            WeighingSessionId = sessionId,
            TicketNo = "PC001",
            RecordRole = WeighTicketRecordRoles.MasterSession,
            Status = TicketStatus.TICKET_COMPLETED,
            Weight1 = 54_000m,
            Weight2 = 14_000m,
            Weight2Time = now.AddMinutes(-5),
            Weight2User = "operator",
            Weight2Mode = WeightMode.AUTO,
            Weight2IsStable = true,
            Weight2UpdatedAt = now.AddMinutes(-5),
            NetWeight = 40_000m,
            IsOverWeight = true
        };
        var delivery = new DeliveryTicket
        {
            Id = deliveryId,
            CutOrderId = cutOrderId,
            WeighingSessionId = sessionId,
            WeighingSessionLineId = lineId,
            DeliveryNo = "PGN001",
            RecordRole = DeliveryTicketRecordRoles.Normal,
            AllocatedWeight = 40_000m,
            AllocatedBagCount = 800,
            IsOverWeight = true
        };

        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { line });
        cutOrderRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { cutOrder });
        weighRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { masterTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { delivery });
        weighRepo.GetAllByCutOrderIdAsync(cutOrderId, Arg.Any<CancellationToken>()).Returns(new[] { masterTicket });
        deliveryRepo.GetAllByCutOrderIdAsync(cutOrderId, Arg.Any<CancellationToken>()).Returns(new[] { delivery });

        var sut = new DeleteSessionWeight2UseCase(sessionRepo, cutOrderRepo, weighRepo, deliveryRepo, uow, currentUser, clock, audit);

        await sut.ExecuteAsync(new DeleteSessionWeight2Request(sessionId, "Sai số cân lần 2"), CancellationToken.None);

        Assert.Equal(WeighingSessionStatus.PENDING_WEIGHT2, session.SessionStatus);
        Assert.Null(session.Weight2);
        Assert.Null(session.Weight2Time);
        Assert.Null(session.NetWeight);
        Assert.False(session.IsOverweight);
        Assert.Equal(0m, session.OverweightAmount);
        Assert.Equal(OverweightResolutionStatus.NOT_APPLICABLE, session.OverweightResolutionStatus);
        Assert.Equal(SyncStatus.SYNC_QUEUED, session.SyncStatus);
        Assert.Null(session.LastSyncAttemptAt);
        Assert.Null(session.LastSyncError);

        Assert.Null(masterTicket.Weight2);
        Assert.Null(masterTicket.Weight2Time);
        Assert.Null(masterTicket.Weight2User);
        Assert.Null(masterTicket.Weight2Mode);
        Assert.Null(masterTicket.Weight2IsStable);
        Assert.Null(masterTicket.Weight2UpdatedAt);
        Assert.Null(masterTicket.NetWeight);
        Assert.Equal(TicketStatus.LOADING_STARTED, masterTicket.Status);
        Assert.Equal(SyncStatus.SYNC_QUEUED, masterTicket.SyncStatus);

        Assert.Null(line.ActualAllocatedWeight);
        Assert.Null(line.ActualAllocatedBagCount);
        Assert.Null(line.BagCountDisplay);
        Assert.Null(line.SystemCalculatedBagCount);
        Assert.Null(line.BagCountConfirmedAt);
        Assert.Null(line.BagCountConfirmedBy);
        Assert.Null(line.BagCountConfirmationMode);
        Assert.False(line.IsReturnedBrokenTrip);
        Assert.Null(line.Note);
        Assert.Equal(WeighingSessionLineStatus.PENDING, line.LineStatus);
        Assert.Null(line.DeliveryTicketId);

        Assert.True(delivery.IsDeleted);
        Assert.Equal(0m, delivery.AllocatedWeight);
        Assert.Equal(0, delivery.AllocatedBagCount);
        Assert.False(delivery.IsOverWeight);
        Assert.Null(cutOrder.CurrentPrimaryDeliveryTicketId);
        Assert.Equal(CutOrderStatus.IN_SESSION, cutOrder.CutOrderStatus);
        Assert.Equal(ProcessingStage.WEIGHING, cutOrder.ProcessingStage);

        await audit.Received(1).LogAsync(
            "DELETE_WEIGHT_2",
            nameof(WeighingSession),
            sessionId,
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BlocksExportFinalizedCutOrder()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var audit = Substitute.For<IAuditService>();
        currentUser.Username.Returns("manager");
        currentUser.RoleCode.Returns("MANAGER");
        var sessionId = Guid.NewGuid();
        var session = new WeighingSession
        {
            Id = sessionId,
            StationCode = "QN01",
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 50_000m,
            Weight2 = 12_000m,
            Weight2Time = DateTime.Now,
            SessionStatus = WeighingSessionStatus.COMPLETED
        };
        var cutOrder = new CutOrder
        {
            Id = Guid.NewGuid(),
            IsExportScale = true,
            ExportFinalizedAt = DateTime.Now,
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.COMPLETED
        };
        sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        cutOrderRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { cutOrder });

        var sut = new DeleteSessionWeight2UseCase(sessionRepo, cutOrderRepo, weighRepo, deliveryRepo, uow, currentUser, clock, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(new DeleteSessionWeight2Request(sessionId, "test"), CancellationToken.None));

        await sessionRepo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }
}
