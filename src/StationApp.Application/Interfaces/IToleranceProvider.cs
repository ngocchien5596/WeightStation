namespace StationApp.Application.Interfaces;

public interface IToleranceProvider
{
    Task<decimal> GetToleranceKgAsync(CancellationToken ct);
}
