namespace StationApp.Application.Interfaces;

public interface ITicketNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken ct);
}
