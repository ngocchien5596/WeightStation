using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class PortTransferUseCaseTests
{
    [Fact]
    public async Task SetWeighingSessionPortTransfer_UpdatesOnlyDomesticOutboundCutOrders()
    {
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 21, 10, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });

        var domestic = new CutOrder
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.OUTBOUND,
            SyncStatus = SyncStatus.SYNC_SUCCESS
        };
        var exportScale = new CutOrder
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.OUTBOUND,
            IsExportScale = true,
            SyncStatus = SyncStatus.SYNC_SUCCESS
        };
        var inbound = new CutOrder
        {
            Id = Guid.NewGuid(),
            TransactionType = TransactionType.INBOUND,
            SyncStatus = SyncStatus.SYNC_SUCCESS
        };

        cutOrderRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([domestic, exportScale, inbound]);

        var sut = new SetWeighingSessionPortTransferUseCase(cutOrderRepo, uow, currentUser, clock);

        await sut.ExecuteAsync(sessionId, enabled: true, CancellationToken.None);

        Assert.True(domestic.IsPortTransfer);
        Assert.Equal(SyncStatus.SYNC_QUEUED, domestic.SyncStatus);
        Assert.Equal(now, domestic.UpdatedAt);
        Assert.Equal("tester", domestic.UpdatedBy);
        Assert.False(exportScale.IsPortTransfer);
        Assert.False(inbound.IsPortTransfer);
        await cutOrderRepo.Received(1).UpdateAsync(domestic, Arg.Any<CancellationToken>());
        await cutOrderRepo.DidNotReceive().UpdateAsync(exportScale, Arg.Any<CancellationToken>());
        await cutOrderRepo.DidNotReceive().UpdateAsync(inbound, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCutOrderPortTransfer_UpdatesSingleDomesticOutboundCutOrder()
    {
        var cutOrderRepo = Substitute.For<ICutOrderRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var cutOrderId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 21, 11, 0, 0);
        currentUser.Username.Returns("tester");
        clock.NowLocal.Returns(now);
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });
        var domestic = new CutOrder
        {
            Id = cutOrderId,
            TransactionType = TransactionType.OUTBOUND,
            SyncStatus = SyncStatus.SYNC_SUCCESS
        };
        cutOrderRepo.GetByIdAsync(cutOrderId, Arg.Any<CancellationToken>()).Returns(domestic);

        var sut = new SetCutOrderPortTransferUseCase(cutOrderRepo, uow, currentUser, clock);

        await sut.ExecuteAsync(cutOrderId, enabled: true, CancellationToken.None);

        Assert.True(domestic.IsPortTransfer);
        Assert.Equal(SyncStatus.SYNC_QUEUED, domestic.SyncStatus);
        Assert.Equal(now, domestic.UpdatedAt);
        Assert.Equal("tester", domestic.UpdatedBy);
        await cutOrderRepo.Received(1).UpdateAsync(domestic, Arg.Any<CancellationToken>());
    }
}
