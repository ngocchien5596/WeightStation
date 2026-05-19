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
        var session = CreateReadySession(netWeight: 22_500m, ttcp10: 22_000m);
        var lines = new[]
        {
            CreateAllocatedLine(weight: 10_000m, bagCount: 200),
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
        var session = CreateReadySession(netWeight: 21_500m, ttcp10: 22_000m);

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
        var session = CreateReadySession(netWeight: 21_500m, ttcp10: 22_000m);
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
    public void BuildSplitPlan_SplitsIntoExactlyTwoTickets_AndPreservesBagCounts()
    {
        var service = new WeighingSessionOverweightService();
        var line1Id = Guid.NewGuid();
        var line2Id = Guid.NewGuid();
        var session = CreateReadySession(netWeight: 24_000m, ttcp10: 22_000m);
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
        var session = CreateReadySession(netWeight: 30_000m, ttcp10: 27_500m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 18_000m, bagCount: 360),
            CreateAllocatedLine(sequenceNo: 2, weight: 12_000m, bagCount: 240)
        };

        var plan = service.BuildSplitPlan(session, lines, 0.0025m);

        Assert.False(plan.IsManualOverride);
        Assert.NotNull(plan.RandomSplitFactor);
        Assert.InRange(plan.RandomSplitFactor!.Value, 0.0001m, 0.0025m);
        Assert.Equal(plan.NetWeight, plan.SplitTicket1NetWeight + plan.SplitTicket2NetWeight);
        Assert.True(plan.SplitTicket1NetWeight < plan.Ttcp10WeightSnapshot);
        Assert.True(plan.SplitTicket2NetWeight <= plan.Ttcp10WeightSnapshot);
    }

    [Fact]
    public void BuildSplitPlan_ManualOverride_UsesRequestedWeight_AndHidesRandomFactor()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 30_000m, ttcp10: 27_500m);
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
    public void BuildSplitPlan_ThrowsWhenSecondTicketStillExceedsThreshold()
    {
        var service = new WeighingSessionOverweightService();
        var session = CreateReadySession(netWeight: 44_500m, ttcp10: 22_000m);
        var lines = new[]
        {
            CreateAllocatedLine(sequenceNo: 1, weight: 22_250m, bagCount: 445),
            CreateAllocatedLine(sequenceNo: 2, weight: 22_250m, bagCount: 445)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => service.BuildSplitPlan(session, lines, 0.0025m));

        Assert.Contains("khÃ´ng thá»ƒ tÃ¡ch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveWeighingSessionOverweightNoSplit_SetsConfirmedStatus()
    {
        var sessionRepo = Substitute.For<IWeighingSessionRepository>();
        var userContext = Substitute.For<ICurrentUserContext>();
        var clock = Substitute.For<IClock>();
        var uow = Substitute.For<IUnitOfWork>();
        var session = CreateReadySession(netWeight: 22_500m, ttcp10: 22_000m);
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

        var session = CreateReadySession(netWeight: 33_000m, ttcp10: 27_500m);
        session.IsOverweight = true;
        session.OverweightAmount = 5_500m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.PENDING;

        var line1Id = Guid.NewGuid();
        var line2Id = Guid.NewGuid();
        var registration1 = Guid.NewGuid();
        var registration2 = Guid.NewGuid();
        var lines = new[]
        {
            CreateAllocatedLine(line1Id, 1, 27_431m, 549, registration1),
            CreateAllocatedLine(line2Id, 2, 5_569m, 111, registration2)
        };

        var masterTicket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration1,
            WeighingSessionId = session.Id,
            TicketNo = "QN26050001",
            VehiclePlate = session.VehiclePlate,
            CustomerName = "Customer A",
            ProductName = "PCB40 CN",
            TransactionType = TransactionType.OUTBOUND,
            Status = TicketStatus.TICKET_COMPLETED,
            RecordRole = WeighTicketRecordRoles.MasterSession,
            Weight1 = 12_000m,
            Weight1User = "tester",
            Weight1Time = new DateTime(2026, 5, 1, 8, 0, 0),
            Weight1UpdatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            Weight1Mode = WeightMode.AUTO,
            Weight1IsStable = true,
            Weight2 = 45_000m,
            Weight2User = "tester",
            Weight2Time = new DateTime(2026, 5, 1, 9, 0, 0),
            Weight2UpdatedAt = new DateTime(2026, 5, 1, 9, 0, 0),
            Weight2Mode = WeightMode.AUTO,
            Weight2IsStable = true,
            NetWeight = 33_000m,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };

        var normalDelivery1 = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration1,
            WeighingSessionId = session.Id,
            WeighingSessionLineId = line1Id,
            DeliveryNo = "DN26050001",
            ErpCutOrderId = "ERP-1",
            CustomerCode = "C1",
            ProductCode = "P1",
            RecordRole = DeliveryTicketRecordRoles.Normal,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };

        var normalDelivery2 = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration2,
            WeighingSessionId = session.Id,
            WeighingSessionLineId = line2Id,
            DeliveryNo = "DN26050002",
            ErpCutOrderId = "ERP-2",
            CustomerCode = "C2",
            ProductCode = "P2",
            RecordRole = DeliveryTicketRecordRoles.Normal,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0),
            CreatedBy = "tester"
        };

        var lineItems = new[]
        {
            new StationApp.Application.DTOs.WeighingSessionLineItem(line1Id, registration1, 1, "ERP-1", "Customer A", "Customer A", "P1", "PCB40 CN", 27_431m, 549, 27_431m, 549, WeighingSessionLineStatus.ALLOCATED, false),
            new StationApp.Application.DTOs.WeighingSessionLineItem(line2Id, registration2, 2, "ERP-2", "Customer B", "Customer B", "P2", "PCB40 CN", 5_569m, 111, 5_569m, 111, WeighingSessionLineStatus.ALLOCATED, false)
        };

        var addedWeighTickets = new List<WeighTicket>();
        var addedDeliveryTickets = new List<DeliveryTicket>();

        sessionRepo.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        sessionRepo.GetLinesBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(lines);
        sessionRepo.GetLineItemsBySessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(lineItems);
        weighRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns([masterTicket]);
        deliveryRepo.GetByWeighingSessionIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns([normalDelivery1, normalDelivery2]);
        configRepo.GetValueAsync(AppConfigKeys.OverweightSplitStepWeight, Arg.Any<CancellationToken>()).Returns("0.0025");
        ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("QN26050010");
        deliveryNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("DN26050020");
        userContext.Username.Returns("supervisor");
        clock.NowLocal.Returns(new DateTime(2026, 5, 1, 11, 30, 0));
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        weighRepo.When(x => x.AddAsync(Arg.Any<WeighTicket>(), Arg.Any<CancellationToken>()))
            .Do(call => addedWeighTickets.Add(call.Arg<WeighTicket>()));
        deliveryRepo.When(x => x.AddAsync(Arg.Any<DeliveryTicket>(), Arg.Any<CancellationToken>()))
            .Do(call => addedDeliveryTickets.Add(call.Arg<DeliveryTicket>()));

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

        Assert.Equal(["QN26050010", "QN26050011"], addedWeighTickets.Select(x => x.TicketNo).ToArray());
        Assert.Equal(addedDeliveryTickets.Count, addedDeliveryTickets.Select(x => x.DeliveryNo).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(0, addedDeliveryTickets.Count).Select(offset => $"DN260500{20 + offset:D2}").ToArray(),
            addedDeliveryTickets.Select(x => x.DeliveryNo).ToArray());
        Assert.Equal(OverweightResolutionStatus.SPLIT_CONFIRMED, session.OverweightResolutionStatus);
    }

    [Theory]
    [InlineData(OverweightResolutionStatus.NOT_APPLICABLE, true)]
    [InlineData(OverweightResolutionStatus.SPLIT_CONFIRMED, true)]
    [InlineData(OverweightResolutionStatus.NO_SPLIT_CONFIRMED, true)]
    [InlineData(OverweightResolutionStatus.PENDING, false)]
    public void CanMoveToOutYard_RequiresResolvedOverweightState(OverweightResolutionStatus status, bool expected)
    {
        var session = CreateReadySession(netWeight: 22_500m, ttcp10: 22_000m);
        session.OverweightResolutionStatus = status;
        session.IsOverweight = status == OverweightResolutionStatus.PENDING;

        var result = CompleteWeighingSessionUseCase.CanMoveToOutYard(session);

        Assert.Equal(expected, result);
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
            Weight2 = 32_000m,
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



