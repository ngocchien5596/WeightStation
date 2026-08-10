using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class WeighingSessionOverweightServiceTests
{
    [Fact]
    public void RefreshSessionOverweightState_SetsPendingWhenNetWeightExceedsThreshold()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 32_500m, ttcp10: 32_000m);
        var lines = new[]
        {
            CreateAllocatedLine(weight: 20_000m, bagCount: 400),
            CreateAllocatedLine(weight: 12_500m, bagCount: 250)
        };

        service.RefreshSessionOverweightState(
            session,
            lines,
            [],
            [],
            new DateTime(2026, 5, 1, 9, 0, 0),
            "tester");

        Assert.True(session.IsOverweight);
        Assert.Equal(500m, session.OverweightAmount);
        Assert.Equal(OverweightResolutionStatus.PENDING, session.OverweightResolutionStatus);
        Assert.Null(session.OverweightResolvedAt);
        Assert.Null(session.OverweightResolvedBy);
    }

    [Fact]
    public void RefreshSessionOverweightState_SetsNotApplicableWhenNetWeightDoesNotExceedThreshold()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 21_500m, ttcp10: 32_000m);

        service.RefreshSessionOverweightState(
            session,
            [CreateAllocatedLine(weight: 21_500m, bagCount: 430)],
            [],
            [],
            new DateTime(2026, 5, 1, 9, 30, 0),
            "tester");

        Assert.False(session.IsOverweight);
        Assert.Equal(0m, session.OverweightAmount);
        Assert.Equal(OverweightResolutionStatus.NOT_APPLICABLE, session.OverweightResolutionStatus);
    }

    [Fact]
    public void RefreshSessionOverweightState_InvalidatesSplitDocuments_WhenResolvedSessionIsReallocated()
    {
        var service = new WeighingSessionOverweightService();
        var now = new DateTime(2026, 5, 1, 10, 15, 0);
        var session = CreateReadySession(netWeight: 21_500m, ttcp10: 32_000m);
        session.OverweightResolutionStatus = OverweightResolutionStatus.SPLIT_CONFIRMED;
        session.OverweightResolvedAt = now.AddMinutes(-30);
        session.OverweightResolvedBy = "old-user";

        var splitWeigh = new WeighTicket
        {
            Id = Guid.NewGuid(),
            RecordRole = WeighTicketRecordRoles.SplitDerived,
            CreatedAt = now.AddHours(-1),
            CreatedBy = "tester"
        };
        var splitDelivery = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            DeliveryNo = "PGN000123",
            ErpCutOrderId = "ERP-001",
            CutOrderId = Guid.NewGuid(),
            RecordRole = DeliveryTicketRecordRoles.SplitDerived,
            CreatedAt = now.AddHours(-1),
            CreatedBy = "tester"
        };

        service.RefreshSessionOverweightState(
            session,
            [CreateAllocatedLine(weight: 21_500m, bagCount: 430)],
            [splitWeigh],
            [splitDelivery],
            now,
            "reallocator");

        Assert.True(splitWeigh.IsDeleted);
        Assert.Equal(now, splitWeigh.DeletedAt);
        Assert.Equal("reallocator", splitWeigh.DeletedBy);

        Assert.True(splitDelivery.IsDeleted);
        Assert.Equal(now, splitDelivery.DeletedAt);
        Assert.Equal("reallocator", splitDelivery.DeletedBy);

        Assert.False(session.IsOverweight);
        Assert.Equal(0m, session.OverweightAmount);
        Assert.Equal(OverweightResolutionStatus.NOT_APPLICABLE, session.OverweightResolutionStatus);
        Assert.Null(session.OverweightResolvedAt);
        Assert.Null(session.OverweightResolvedBy);
    }

    [Fact]
    public void BuildSplitPlan_OverweightUnder1xThreshold_SplitsBothTicketsWithinTtcp10()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 36_000m, ttcp10: 33_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 18_000m, bagCount: 360),
            CreateAllocatedLine(sequenceNo: 2, weight: 18_000m, bagCount: 360)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m);

        Assert.Equal(36_000m, plan.NetWeight);
        Assert.Equal(33_000m, plan.Ttcp10WeightSnapshot);
        Assert.Equal(36_000m, plan.SplitTicket1NetWeight + plan.SplitTicket2NetWeight);
        Assert.True(plan.SplitTicket1NetWeight <= 33_000m);
        Assert.True(plan.SplitTicket2NetWeight <= 33_000m);
    }

    [Fact]
    public void BuildSplitPlan_OverweightExactly2xThreshold_SplitsBothTicketsEquallyAtTtcp10Limit()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 66_000m, ttcp10: 33_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 33_000m, bagCount: 660),
            CreateAllocatedLine(sequenceNo: 2, weight: 33_000m, bagCount: 660)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m, 33_000m, true);

        Assert.Equal(66_000m, plan.NetWeight);
        Assert.Equal(33_000m, plan.SplitTicket1NetWeight);
        Assert.Equal(33_000m, plan.SplitTicket2NetWeight);
        Assert.True(plan.SplitTicket1NetWeight <= 33_000m);
        Assert.True(plan.SplitTicket2NetWeight <= 33_000m);
    }

    [Fact]
    public void BuildSplitPlan_OverweightOver1xThreshold_Ticket1GetsTtcp10Limit_Ticket2GetsResidual()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 75_000m, ttcp10: 33_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 37_500m, bagCount: 750),
            CreateAllocatedLine(sequenceNo: 2, weight: 37_500m, bagCount: 750)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m);

        Assert.Equal(75_000m, plan.NetWeight);
        Assert.Equal(33_000m, plan.SplitTicket1NetWeight);
        Assert.Equal(42_000m, plan.SplitTicket2NetWeight);
        Assert.Equal(75_000m, plan.Groups.Sum(x => x.GroupWeight));
    }

    [Fact]
    public void BuildSplitPlan_SplitsIntoExactlyTwoTickets_AndPreservesBagCounts()
    {
        var service = new WeighingSessionOverweightService();
        var line1Id = Guid.NewGuid();
        var line2Id = Guid.NewGuid();
        var session = CreateReadySession(netWeight: 24_000m, ttcp10: 32_000m);
        var lines = new[]
        {
            CreateAllocatedLine(line1Id, sequenceNo: 1, weight: 12_000m, bagCount: 120),
            CreateAllocatedLine(line2Id, sequenceNo: 2, weight: 12_000m, bagCount: 120)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m, 21_945m, true);

        Assert.Equal(2, plan.Groups.Count);
        Assert.Equal(21_945m, plan.Groups[0].GroupWeight);
        Assert.Equal(2_055m, plan.Groups[1].GroupWeight);
        Assert.Equal(24_000m, plan.Groups.Sum(x => x.GroupWeight));
        Assert.Equal(0.0025m, plan.OverweightSplitStepWeight);
        Assert.True(plan.IsManualOverride);

        var line1Parts = plan.Groups.SelectMany(x => x.Lines).Where(x => x.SessionLineId == line1Id).ToList();
        var line2Parts = plan.Groups.SelectMany(x => x.Lines).Where(x => x.SessionLineId == line2Id).ToList();

        Assert.Single(line1Parts);
        Assert.Equal(12_000m, line1Parts[0].AllocatedWeight);
        Assert.Equal(120, line1Parts[0].AllocatedBagCount);

        Assert.Equal(2, line2Parts.Count);
        Assert.Equal(9_945m, line2Parts[0].AllocatedWeight);
        Assert.Equal(2_055m, line2Parts[1].AllocatedWeight);
        Assert.Equal(99, line2Parts[0].AllocatedBagCount);
        Assert.Equal(21, line2Parts[1].AllocatedBagCount);
        Assert.Equal(120, line2Parts.Sum(x => x.AllocatedBagCount ?? 0));
    }

    [Fact]
    public void BuildSplitPlan_SystemSuggestion_UsesRandomFactorWithinConfiguredRange()
    {
        var service = new WeighingSessionOverweightService();
        // netWeight=36_000 > ttcp10=33_000 (overweight), and 36_000 <= 2*33_000=66_000 (both tickets can stay <= ttcp10)
        var session = CreateReadySession(netWeight: 36_000m, ttcp10: 33_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 18_000m, bagCount: 360),
            CreateAllocatedLine(sequenceNo: 2, weight: 18_000m, bagCount: 360)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m);

        Assert.False(plan.IsManualOverride);
        Assert.NotNull(plan.RandomSplitFactor);
        Assert.InRange(plan.RandomSplitFactor!.Value, 0.0001m, 0.0025m);
        Assert.Equal(plan.NetWeight, plan.SplitTicket1NetWeight + plan.SplitTicket2NetWeight);
        Assert.True(plan.SplitTicket1NetWeight % 10m == 0m);
        Assert.True(plan.SplitTicket1NetWeight <= plan.Ttcp10WeightSnapshot);
        Assert.True(plan.SplitTicket2NetWeight <= plan.Ttcp10WeightSnapshot);
    }

    [Fact]
    public void BuildSplitPlan_ManualOverride_UsesRequestedWeight_AndHidesRandomFactor()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 30_000m, ttcp10: 37_500m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 18_000m, bagCount: 360),
            CreateAllocatedLine(sequenceNo: 2, weight: 12_000m, bagCount: 240)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m, 26_000m, true);

        Assert.True(plan.IsManualOverride);
        Assert.Null(plan.RandomSplitFactor);
        Assert.Equal(26_000m, plan.SplitTicket1NetWeight);
        Assert.Equal(4_000m, plan.SplitTicket2NetWeight);
    }

    [Fact]
    public void BuildSplitPlan_AllowsResidualTicketToExceedThreshold_WhenFirstTicketNetEqualsTtcp10()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 65_500m, ttcp10: 32_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 32_750m, bagCount: 655),
            CreateAllocatedLine(sequenceNo: 2, weight: 32_750m, bagCount: 655)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m);

        Assert.Equal(32_000m, plan.SplitTicket1NetWeight);
        Assert.Equal(33_500m, plan.SplitTicket2NetWeight);
        Assert.Equal(session.Ttcp10WeightSnapshot, plan.SplitTicket1NetWeight);
        Assert.Equal(32_000m, plan.Groups[0].GroupWeight);
        Assert.Equal(33_500m, plan.Groups[1].GroupWeight);
        Assert.Equal(65_500m, plan.Groups.Sum(x => x.GroupWeight));
    }

    [Fact]
    public async Task ResolveWeighingSessionOverweightNoSplit_SetsConfirmedStatus()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var userContext = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var session = CreateReadySession(netWeight: 32_500m, ttcp10: 32_000m);
        session.IsOverweight = true;
        session.OverweightAmount = 500m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.PENDING;

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        userContext.Username.Returns("supervisor");
        clock.NowLocal.Returns(new DateTime(2026, 5, 1, 11, 0, 0));
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        var useCase = new ResolveWeighingSessionOverweightNoSplitUseCase(sessionRepo, userContext, clock, uow);

        await useCase.ExecuteAsync(session.Id, CancellationToken.None);

        Assert.Equal(OverweightResolutionStatus.NO_SPLIT_CONFIRMED, session.OverweightResolutionStatus);
        Assert.Equal("supervisor", session.OverweightResolvedBy);
        Assert.Equal(new DateTime(2026, 5, 1, 11, 0, 0), session.OverweightResolvedAt);
        await sessionRepo.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveWeighingSessionOverweightSplit_AssignsDistinctTicketAndDeliveryNumbers()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var weighRepo = Substitute.For<IWeighTicketRepository>();
        var deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
        var configRepo = Substitute.For<IAppConfigRepository>();
        var ticketNoGen = Substitute.For<ITicketNumberGenerator>();
        var deliveryNoGen = Substitute.For<IDeliveryNumberGenerator>();
        var userContext = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var service = new WeighingSessionOverweightService();

        var session = CreateReadySession(netWeight: 33_000m, ttcp10: 30_000m);
        session.IsOverweight = true;
        session.OverweightAmount = 3_000m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.PENDING;

        var line = CreateAllocatedLine(weight: 33_000m, bagCount: 660);
        var masterWeighTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            RecordRole = WeighTicketRecordRoles.MasterSession,
            TicketNo = "QN26050001",
            Weight1 = 10_000m,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };
        var normalDelivery = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            DeliveryNo = "DN26050001",
            ErpCutOrderId = "ERP-001",
            CutOrderId = line.CutOrderId,
            WeighingSessionLineId = line.Id,
            RecordRole = DeliveryTicketRecordRoles.Normal,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { line });
        sessionRepo.GetLineItemsBySessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new StationApp.Application.DTOs.WeighingSessionLineItem[]
            {
                new(
                    SessionLineId: line.Id,
                    CutOrderId: line.CutOrderId,
                    SequenceNo: line.SequenceNo,
                    ErpCutOrderId: "ERP-001",
                    CustomerName: "Customer A",
                    DistributorName: null,
                    ProductCode: "SP01",
                    ProductName: "Sản phẩm 01",
                    PlannedWeight: 33_000m,
                    PlannedBagCount: 660,
                    ActualAllocatedWeight: line.ActualAllocatedWeight,
                    ActualAllocatedBagCount: line.ActualAllocatedBagCount,
                    BagCountDisplay: line.ActualAllocatedBagCount,
                    LineStatus: WeighingSessionLineStatus.ALLOCATED,
                    HasPrintedDeliveryTicket: false)
            });

        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { masterWeighTicket });
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { normalDelivery });

        configRepo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, Arg.Any<CancellationToken>())
            .Returns("0.0025");

        var ticketSeq = 10;
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult($"QN260500{ticketSeq++}"));

        var deliverySeq = 20;
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult($"DN260500{deliverySeq++:D2}"));

        userContext.Username.Returns("operator");
        clock.NowLocal.Returns(new DateTime(2026, 5, 1, 10, 0, 0));

        var addedWeighTickets = new List<WeighTicket>();
        weighRepo.AddAsync(Arg.Do<WeighTicket>(addedWeighTickets.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var addedDeliveryTickets = new List<DeliveryTicket>();
        deliveryRepo.AddAsync(Arg.Do<DeliveryTicket>(addedDeliveryTickets.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        var useCase = new ResolveWeighingSessionOverweightSplitUseCase(
            sessionRepo,
            weighRepo,
            deliveryRepo,
            configRepo,
            ticketNoGen,
            deliveryNoGen,
            userContext,
            clock,
            uow,
            service);

        await useCase.ExecuteAsync(session.Id, CancellationToken.None);

        Assert.Equal(OverweightResolutionStatus.SPLIT_CONFIRMED, session.OverweightResolutionStatus);
        Assert.True(addedWeighTickets.Count > 0);
        Assert.Equal(addedDeliveryTickets.Count, addedDeliveryTickets.Select(x => x.DeliveryNo).Distinct().Count());
    }

    [Theory]
    [InlineData(OverweightResolutionStatus.NOT_APPLICABLE, true)]
    [InlineData(OverweightResolutionStatus.SPLIT_CONFIRMED, true)]
    [InlineData(OverweightResolutionStatus.NO_SPLIT_CONFIRMED, true)]
    [InlineData(OverweightResolutionStatus.PENDING, false)]
    public void CanMoveToOutYard_RequiresResolvedOverweightState(OverweightResolutionStatus status, bool expected)
    {
        var session = CreateReadySession(netWeight: 22_500m, ttcp10: 32_000m);
        session.OverweightResolutionStatus = status;
        session.IsOverweight = status == OverweightResolutionStatus.PENDING;

        var result = CompleteWeighingSessionUseCase.CanMoveToOutYard(session);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RefreshSessionOverweightState_CalculatesOverweightAmountFromNetWeight()
    {
        var service = new WeighingSessionOverweightService();
        var session = new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = "WS-002",
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = "14C-5555",
            SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE,
            Weight1 = 12_000m,
            Weight2 = 57_000m,
            NetWeight = 45_000m,
            Ttcp10WeightSnapshot = 40_000m,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };
        var lines = new[]
        {
            CreateAllocatedLine(weight: 45_000m, bagCount: 900)
        };

        service.RefreshSessionOverweightState(
            session,
            lines,
            [],
            [],
            new DateTime(2026, 5, 1, 9, 0, 0),
            "tester");

        Assert.True(session.IsOverweight);
        Assert.Equal(5_000m, session.OverweightAmount);
        Assert.Equal(OverweightResolutionStatus.PENDING, session.OverweightResolutionStatus);
    }

    private static WeighingSession CreateReadySession(decimal netWeight, decimal ttcp10)
    {
        return new WeighingSession
        {
            Id = Guid.NewGuid(),
            SessionNo = "WS-001",
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = "51C-12345",
            SessionStatus = WeighingSessionStatus.READY_TO_COMPLETE,
            Weight1 = 10_000m,
            Weight2 = 10_000m + netWeight,
            NetWeight = netWeight,
            Ttcp10WeightSnapshot = ttcp10,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };
    }

    private static WeighingSessionLine CreateAllocatedLine(decimal weight, int? bagCount)
        => CreateAllocatedLine(Guid.NewGuid(), sequenceNo: 1, weight, bagCount);

    private static WeighingSessionLine CreateAllocatedLine(int sequenceNo, decimal weight, int? bagCount)
        => CreateAllocatedLine(Guid.NewGuid(), sequenceNo, weight, bagCount);

    private static WeighingSessionLine CreateAllocatedLine(Guid lineId, int sequenceNo, decimal weight, int? bagCount, Guid vehicleCutOrderId)
    {
        return new WeighingSessionLine
        {
            Id = lineId,
            WeighingSessionId = Guid.NewGuid(),
            CutOrderId = vehicleCutOrderId,
            SequenceNo = sequenceNo,
            ActualAllocatedWeight = weight,
            ActualAllocatedBagCount = bagCount,
            LineStatus = WeighingSessionLineStatus.ALLOCATED,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };
    }

    private static WeighingSessionLine CreateAllocatedLine(Guid lineId, int sequenceNo, decimal weight, int? bagCount)
    {
        return new WeighingSessionLine
        {
            Id = lineId,
            WeighingSessionId = Guid.NewGuid(),
            CutOrderId = Guid.NewGuid(),
            SequenceNo = sequenceNo,
            ActualAllocatedWeight = weight,
            ActualAllocatedBagCount = bagCount,
            LineStatus = WeighingSessionLineStatus.ALLOCATED,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };
    }
}
