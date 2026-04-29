using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface ISyncPayloadFactory
{
    string CreatePayload(WeighTicket ticket);
    string CreatePayload(VehicleRegistration registration);
}
