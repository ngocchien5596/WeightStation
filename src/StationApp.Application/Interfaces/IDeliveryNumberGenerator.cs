namespace StationApp.Application.Interfaces;

public interface IDeliveryNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken ct);
}
