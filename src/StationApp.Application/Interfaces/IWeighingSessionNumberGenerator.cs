using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionNumberGenerator
{
    Task<string> GenerateAsync(TransactionType transactionType, CancellationToken ct);
}
