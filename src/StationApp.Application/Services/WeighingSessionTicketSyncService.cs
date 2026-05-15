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
}

public sealed record WeightCaptureSnapshot(
    string Username,
    WeightMode Mode,
    bool IsStable
);
