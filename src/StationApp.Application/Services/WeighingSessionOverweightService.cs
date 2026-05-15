using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Services;

public sealed class WeighingSessionOverweightService
{
    private const decimal MinRandomSplitFactor = 0.0001m;
    private const decimal MinPositiveWeight = 0.001m;
    private const int MaxRandomSuggestionAttempts = 50;
    private const string InvalidSplitMessage = "Luot can nay khong the tach hop le thanh 2 phieu voi nguong TTCP 10% hien tai. Vui long chon Khong tach hoac kiem tra lai tham so tach tai.";

    public decimal ResolveTtcp10Threshold(decimal? baseTtcpWeight, IReadOnlyCollection<WeighingSessionLine> lines)
    {
        var baseWeight = baseTtcpWeight.GetValueOrDefault();
        if (baseWeight <= 0m)
        {
            baseWeight = lines.Sum(x => x.PlannedWeight ?? 0m);
        }

        if (baseWeight <= 0m)
        {
            throw new InvalidOperationException("Khong xac dinh duoc nguong TTCP 10% cho luot can.");
        }

        return decimal.Round(baseWeight * 1.10m, 3, MidpointRounding.AwayFromZero);
    }

    public void RefreshSessionOverweightState(
        WeighingSession session,
        IReadOnlyList<WeighingSessionLine> lines,
        IReadOnlyList<WeighTicket> weighTickets,
        IReadOnlyList<DeliveryTicket> deliveryTickets,
        DateTime now,
        string username)
    {
        InvalidateResolvedDocumentsIfNeeded(session, weighTickets, deliveryTickets, now, username);

        var canEvaluate =
            session.Weight1.HasValue &&
            session.Weight2.HasValue &&
            session.NetWeight.HasValue &&
            session.Ttcp10WeightSnapshot.HasValue &&
            lines.Count > 0 &&
            lines.All(x => x.LineStatus == WeighingSessionLineStatus.ALLOCATED && x.ActualAllocatedWeight.HasValue);

        if (!canEvaluate)
        {
            session.IsOverweight = false;
            session.OverweightAmount = 0m;
            session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
            session.OverweightResolvedAt = null;
            session.OverweightResolvedBy = null;
            return;
        }

        var overweightAmount = decimal.Round(
            session.NetWeight!.Value - session.Ttcp10WeightSnapshot!.Value,
            3,
            MidpointRounding.AwayFromZero);

        if (overweightAmount > 0m)
        {
            session.IsOverweight = true;
            session.OverweightAmount = overweightAmount;
            session.OverweightResolutionStatus = OverweightResolutionStatus.PENDING;
            session.OverweightResolvedAt = null;
            session.OverweightResolvedBy = null;
            return;
        }

        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
    }

    public OverweightSplitPlan BuildSplitPlan(
        WeighingSession session,
        IReadOnlyList<WeighingSessionLine> lines,
        decimal splitStepWeight,
        decimal? firstSplitNetWeight = null,
        bool isManualOverride = false)
    {
        if (!session.NetWeight.HasValue || !session.Ttcp10WeightSnapshot.HasValue)
        {
            throw new InvalidOperationException("Luot can chua du du lieu de lap phuong an tach qua tai.");
        }

        var target = session.Ttcp10WeightSnapshot.Value;
        if (target <= 0m)
        {
            throw new InvalidOperationException("Nguong TTCP 10% khong hop le.");
        }

        var sourceLines = lines
            .Where(x => x.ActualAllocatedWeight.GetValueOrDefault() > 0m)
            .OrderBy(x => x.SequenceNo)
            .ToList();

        if (sourceLines.Count == 0)
        {
            throw new InvalidOperationException("Khong co dong phan bo thuc giao de tach qua tai.");
        }

        if (!TryGetFeasibleWeightRange(session.NetWeight.Value, target, out var lowerBound, out var upperBound))
        {
            throw new InvalidOperationException(InvalidSplitMessage);
        }

        decimal resolvedFirstGroupTarget;
        decimal? randomSplitFactor = null;

        if (isManualOverride)
        {
            if (!firstSplitNetWeight.HasValue)
            {
                throw new InvalidOperationException("Phuong an tach tay chua co khoi luong phieu 1.");
            }

            resolvedFirstGroupTarget = decimal.Round(firstSplitNetWeight.Value, 3, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (splitStepWeight < MinRandomSplitFactor || splitStepWeight >= 1m)
            {
                throw new InvalidOperationException("Tham so buoc tach qua tai khong hop le.");
            }

            if (firstSplitNetWeight.HasValue)
            {
                resolvedFirstGroupTarget = decimal.Round(firstSplitNetWeight.Value, 3, MidpointRounding.AwayFromZero);
            }
            else
            {
                (resolvedFirstGroupTarget, randomSplitFactor) = BuildSuggestedFirstSplitWeight(
                    session.NetWeight.Value,
                    target,
                    splitStepWeight,
                    lowerBound,
                    upperBound);
            }
        }

        var firstGroupTarget = resolvedFirstGroupTarget;
        var secondGroupTarget = decimal.Round(session.NetWeight.Value - firstGroupTarget, 3, MidpointRounding.AwayFromZero);
        ValidateSplitTargets(target, firstGroupTarget, secondGroupTarget, lowerBound, upperBound);

        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var remainingFirstGroupCapacity = firstGroupTarget;
        var parts = new List<MutableSplitPart>();

        foreach (var line in sourceLines)
        {
            var remainingLineWeight = line.ActualAllocatedWeight!.Value;

            while (remainingLineWeight > 0m)
            {
                var assignToFirstGroup = remainingFirstGroupCapacity > 0m;
                var partWeight = decimal.Round(
                    Math.Min(remainingLineWeight, assignToFirstGroup ? remainingFirstGroupCapacity : remainingLineWeight),
                    3,
                    MidpointRounding.AwayFromZero);

                if (partWeight <= 0m)
                {
                    throw new InvalidOperationException("Khong the tao phuong an tach qua tai hop le.");
                }

                parts.Add(new MutableSplitPart(
                    assignToFirstGroup ? firstGroupId : secondGroupId,
                    assignToFirstGroup ? (byte)1 : (byte)2,
                    line.Id,
                    line.SequenceNo,
                    line.VehicleRegistrationId,
                    partWeight));

                remainingLineWeight = decimal.Round(remainingLineWeight - partWeight, 3, MidpointRounding.AwayFromZero);
                if (assignToFirstGroup)
                {
                    remainingFirstGroupCapacity = decimal.Round(
                        remainingFirstGroupCapacity - partWeight,
                        3,
                        MidpointRounding.AwayFromZero);
                }
            }
        }

        foreach (var lineParts in parts.GroupBy(x => x.SessionLineId))
        {
            var sourceLine = sourceLines.First(x => x.Id == lineParts.Key);
            AssignBagCounts(sourceLine, lineParts.OrderBy(x => x.GroupSequence).ToList());
        }

        var groups = parts
            .GroupBy(x => new { x.GroupId, x.GroupSequence })
            .OrderBy(x => x.Key.GroupSequence)
            .Select(g => new OverweightSplitGroupPlan(
                g.Key.GroupId,
                g.Key.GroupSequence,
                decimal.Round(g.Sum(x => x.Weight), 3, MidpointRounding.AwayFromZero),
                g.Select(x => new OverweightSplitLinePlan(
                    x.SessionLineId,
                    x.SequenceNo,
                    x.VehicleRegistrationId,
                    x.Weight,
                    x.BagCount)).ToList()))
            .ToList();

        return new OverweightSplitPlan(
            session.Id,
            target,
            session.NetWeight.Value,
            splitStepWeight,
            firstGroupTarget,
            secondGroupTarget,
            randomSplitFactor,
            isManualOverride,
            groups);
    }

    public bool TryGetFeasibleWeightRange(decimal netWeight, decimal ttcp10WeightSnapshot, out decimal lowerBound, out decimal upperBound)
    {
        lowerBound = decimal.Round(Math.Max(netWeight - ttcp10WeightSnapshot, MinPositiveWeight), 3, MidpointRounding.AwayFromZero);
        upperBound = decimal.Round(Math.Min(ttcp10WeightSnapshot - MinPositiveWeight, netWeight - MinPositiveWeight), 3, MidpointRounding.AwayFromZero);
        return lowerBound <= upperBound;
    }

    private static void ValidateSplitTargets(
        decimal ttcp10WeightSnapshot,
        decimal firstGroupTarget,
        decimal secondGroupTarget,
        decimal lowerBound,
        decimal upperBound)
    {
        if (firstGroupTarget < lowerBound
            || firstGroupTarget > upperBound
            || firstGroupTarget <= 0m
            || firstGroupTarget >= ttcp10WeightSnapshot
            || secondGroupTarget <= 0m
            || secondGroupTarget > ttcp10WeightSnapshot)
        {
            throw new InvalidOperationException(InvalidSplitMessage);
        }
    }

    private static (decimal FirstGroupTarget, decimal? RandomSplitFactor) BuildSuggestedFirstSplitWeight(
        decimal netWeight,
        decimal ttcp10WeightSnapshot,
        decimal splitStepWeight,
        decimal lowerBound,
        decimal upperBound)
    {
        var random = Random.Shared;

        for (var attempt = 0; attempt < MaxRandomSuggestionAttempts; attempt++)
        {
            var rawFactor = random.NextDouble();
            var factor = decimal.Round(
                MinRandomSplitFactor + ((decimal)rawFactor * (splitStepWeight - MinRandomSplitFactor)),
                4,
                MidpointRounding.AwayFromZero);
            var candidate = decimal.Round(
                ttcp10WeightSnapshot * (1m - factor),
                3,
                MidpointRounding.AwayFromZero);
            var secondGroupTarget = decimal.Round(netWeight - candidate, 3, MidpointRounding.AwayFromZero);

            if (candidate >= lowerBound
                && candidate <= upperBound
                && secondGroupTarget > 0m
                && secondGroupTarget <= ttcp10WeightSnapshot)
            {
                return (candidate, factor);
            }
        }

        var integerLowerBound = (int)Math.Ceiling(lowerBound);
        var integerUpperBound = (int)Math.Floor(upperBound);
        if (integerLowerBound > integerUpperBound)
        {
            throw new InvalidOperationException(InvalidSplitMessage);
        }

        var fallbackWeight = random.Next(integerLowerBound, integerUpperBound + 1);
        var fallbackFactor = decimal.Round(
            (ttcp10WeightSnapshot - fallbackWeight) / ttcp10WeightSnapshot,
            4,
            MidpointRounding.AwayFromZero);

        return (decimal.Round(fallbackWeight, 3, MidpointRounding.AwayFromZero), fallbackFactor);
    }

    private static void InvalidateResolvedDocumentsIfNeeded(
        WeighingSession session,
        IReadOnlyList<WeighTicket> weighTickets,
        IReadOnlyList<DeliveryTicket> deliveryTickets,
        DateTime now,
        string username)
    {
        var wasResolved =
            session.OverweightResolutionStatus == OverweightResolutionStatus.SPLIT_CONFIRMED ||
            session.OverweightResolutionStatus == OverweightResolutionStatus.NO_SPLIT_CONFIRMED;

        if (!wasResolved)
        {
            return;
        }

        foreach (var weighTicket in weighTickets.Where(x => x.RecordRole == "SPLIT_DERIVED" && !x.IsDeleted))
        {
            weighTicket.IsDeleted = true;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = username;
        }

        foreach (var deliveryTicket in deliveryTickets.Where(x => x.RecordRole == "SPLIT_DERIVED" && !x.IsDeleted))
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = username;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = username;
        }
    }

    private static void AssignBagCounts(WeighingSessionLine sourceLine, IReadOnlyList<MutableSplitPart> parts)
    {
        if (!sourceLine.ActualAllocatedBagCount.HasValue || parts.Count == 0)
        {
            return;
        }

        var totalWeight = sourceLine.ActualAllocatedWeight.GetValueOrDefault();
        if (totalWeight <= 0m)
        {
            return;
        }

        var targetBagCount = sourceLine.ActualAllocatedBagCount.Value;
        var allocations = parts
            .Select(part =>
            {
                var exact = targetBagCount * (part.Weight / totalWeight);
                var floor = (int)Math.Floor(exact);
                return new BagAllocation(part, floor, exact - floor);
            })
            .ToList();

        var remaining = targetBagCount - allocations.Sum(x => x.BagCount);
        foreach (var allocation in allocations
                     .OrderByDescending(x => x.Remainder)
                     .ThenBy(x => x.Part.GroupSequence)
                     .ThenBy(x => x.Part.SequenceNo)
                     .Take(remaining))
        {
            allocation.BagCount++;
        }

        foreach (var allocation in allocations)
        {
            allocation.Part.BagCount = allocation.BagCount;
        }
    }

    private sealed class MutableSplitPart
    {
        public MutableSplitPart(Guid groupId, byte groupSequence, Guid sessionLineId, int sequenceNo, Guid vehicleRegistrationId, decimal weight)
        {
            GroupId = groupId;
            GroupSequence = groupSequence;
            SessionLineId = sessionLineId;
            SequenceNo = sequenceNo;
            VehicleRegistrationId = vehicleRegistrationId;
            Weight = weight;
        }

        public Guid GroupId { get; }
        public byte GroupSequence { get; }
        public Guid SessionLineId { get; }
        public int SequenceNo { get; }
        public Guid VehicleRegistrationId { get; }
        public decimal Weight { get; }
        public int? BagCount { get; set; }
    }

    private sealed class BagAllocation
    {
        public BagAllocation(MutableSplitPart part, int bagCount, decimal remainder)
        {
            Part = part;
            BagCount = bagCount;
            Remainder = remainder;
        }

        public MutableSplitPart Part { get; }
        public int BagCount { get; set; }
        public decimal Remainder { get; }
    }
}

public sealed record OverweightSplitPlan(
    Guid SessionId,
    decimal Ttcp10WeightSnapshot,
    decimal NetWeight,
    decimal OverweightSplitStepWeight,
    decimal SplitTicket1NetWeight,
    decimal SplitTicket2NetWeight,
    decimal? RandomSplitFactor,
    bool IsManualOverride,
    IReadOnlyList<OverweightSplitGroupPlan> Groups);

public sealed record OverweightSplitGroupPlan(
    Guid GroupId,
    byte SplitSequence,
    decimal GroupWeight,
    IReadOnlyList<OverweightSplitLinePlan> Lines);

public sealed record OverweightSplitLinePlan(
    Guid SessionLineId,
    int SequenceNo,
    Guid VehicleRegistrationId,
    decimal AllocatedWeight,
    int? AllocatedBagCount);
