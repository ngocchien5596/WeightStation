using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class GetWeighingSessionsUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;

    public GetWeighingSessionsUseCase(IWeighingSessionRepository sessionRepo)
    {
        _sessionRepo = sessionRepo;
    }

    public Task<IReadOnlyList<WeighingSessionListItem>> ExecuteAsync(string? keyword, TransactionType? transactionType, CancellationToken ct)
    {
        return _sessionRepo.SearchActiveSessionsAsync(keyword, transactionType, ct);
    }
}
