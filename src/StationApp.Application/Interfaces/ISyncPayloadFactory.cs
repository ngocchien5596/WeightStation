using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface ISyncPayloadFactory
{
    string CreatePayload(WeighTicket ticket);
    string CreatePayload(CutOrder registration);
    string CreatePayload(DeliveryTicket ticket);
    string CreatePayload(Vehicle vehicle);
    string CreatePayload(Customer customer);
    string CreatePayload(Product product);
}

