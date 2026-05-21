namespace StationApp.Application.Interfaces;

public interface IToleranceProvider
{
    Task<decimal> GetToleranceKgPerBagAsync(CancellationToken ct);
}
