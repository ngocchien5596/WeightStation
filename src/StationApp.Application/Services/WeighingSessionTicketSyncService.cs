using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Services;

public sealed class WeighingSessionTicketSyncService
{
    public void SyncMasterTicketFromSession(
        WeighingSession session,
        WeighTicket masterTicket,
        DateTime now,
        string username,
        WeightCaptureSnapshot? weight1Snapshot = null,
        WeightCaptureSnapshot? weight2Snapshot = null)
    {
        masterTicket.Weight1 = session.Weight1;
        masterTicket.Weight1Time = session.Weight1Time;
        if (weight1Snapshot != null)
        {
            masterTicket.Weight1User = weight1Snapshot.Username;
            masterTicket.Weight1Mode = weight1Snapshot.Mode;
            masterTicket.Weight1IsStable = weight1Snapshot.IsStable;
            masterTicket.Weight1UpdatedAt = now;
        }

        masterTicket.Weight2 = session.Weight2;
        masterTicket.Weight2Time = session.Weight2Time;
        if (weight2Snapshot != null)
        {
            masterTicket.Weight2User = weight2Snapshot.Username;
            masterTicket.Weight2Mode = weight2Snapshot.Mode;
            masterTicket.Weight2IsStable = weight2Snapshot.IsStable;
            masterTicket.Weight2UpdatedAt = now;
        }

        masterTicket.NetWeight = session.NetWeight;
        masterTicket.Ttcp10WeightSnapshot = session.Ttcp10WeightSnapshot;
        masterTicket.IsOverWeight = session.IsOverweight;
        masterTicket.UpdatedAt = now;
        masterTicket.UpdatedBy = username;
    }

    public void SyncLineTicketFromSession(
        WeighingSession session,
        WeighingSessionLine line,
        CutOrder registration,
        WeighTicket lineTicket,
        decimal startWeight,
        DateTime now,
        string username)
    {
        lineTicket.CutOrderId = registration.Id;
        lineTicket.WeighingSessionId = session.Id;
        lineTicket.ErpCutOrderId = registration.ErpCutOrderId;
        lineTicket.VehiclePlate = session.VehiclePlate;
        lineTicket.MoocNumber = session.MoocNumber;
        lineTicket.DriverName = session.DriverName;
        lineTicket.CustomerCode = registration.CustomerCode;
        lineTicket.CustomerName = registration.CustomerName;
        lineTicket.ProductCode = registration.ProductCode;
        lineTicket.ProductName = registration.ProductName;
        lineTicket.PlannedWeight = registration.PlannedWeight;
        lineTicket.BagCount = registration.BagCount;
        lineTicket.Notes = registration.Notes;
        lineTicket.TransactionType = session.TransactionType;
        lineTicket.TransportMethod = registration.TransportMethod;
        lineTicket.Status = TicketStatus.TICKET_COMPLETED;
        lineTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
        lineTicket.IsDeleted = false;
        lineTicket.IsCancelled = false;
        lineTicket.Weight1 = startWeight;
        lineTicket.Weight1User = lineTicket.Weight1User ?? username;
        lineTicket.Weight1Time = session.Weight1Time;
        var allocatedWeight = line.ActualAllocatedWeight ?? 0m;
        lineTicket.Weight2 = session.TransactionType == TransactionType.OUTBOUND
            ? decimal.Round(startWeight + allocatedWeight, 3, MidpointRounding.AwayFromZero)
            : decimal.Round(startWeight - allocatedWeight, 3, MidpointRounding.AwayFromZero);
        lineTicket.Weight2User = lineTicket.Weight2User ?? username;
        lineTicket.Weight2Time = session.Weight2Time;
        lineTicket.NetWeight = line.ActualAllocatedWeight;
        lineTicket.Ttcp10WeightSnapshot = session.Ttcp10WeightSnapshot;
        lineTicket.IsOverWeight = session.IsOverweight;
        lineTicket.IsPrimaryDisplay = true;
        lineTicket.UpdatedAt = now;
        lineTicket.UpdatedBy = username;
    }
}

public sealed record WeightCaptureSnapshot(
    string Username,
    WeightMode Mode,
    bool IsStable
);
