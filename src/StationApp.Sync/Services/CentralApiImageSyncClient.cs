using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;

namespace StationApp.Sync.Services;

public sealed class CentralApiImageSyncClient : IWeighingSessionImageSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CentralApiImageSyncClient> _logger;

    public CentralApiImageSyncClient(
        HttpClient httpClient,
        IServiceScopeFactory scopeFactory,
        ILogger<CentralApiImageSyncClient> logger)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SyncWeighTicketResponse> PushImageAsync(WeighingSessionImage image, CancellationToken ct)
    {
        var route = await ResolveRouteAsync(image.StationCode, ct);
        if (route.BaseUri == null)
        {
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = "CONFIG_INVALID",
                ErrorMessage = route.ErrorMessage ?? "Sync API URL chua duoc cau hinh hop le."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route.BaseUri, "api/weighing-session-images"));
            request.Headers.Add("Idempotency-Key", image.Id.ToString());
            if (!string.IsNullOrWhiteSpace(route.ApiKey))
            {
                request.Headers.Remove("X-Api-Key");
                request.Headers.Add("X-Api-Key", route.ApiKey);
            }
            request.Content = JsonContent.Create(new SyncWeighingSessionImageRequest
            {
                Id = image.Id,
                StationCode = image.StationCode,
                WeighingSessionId = image.WeighingSessionId,
                CaptureStage = image.CaptureStage.ToString(),
                CameraCode = image.CameraCode,
                CameraName = image.CameraName,
                RtspUrlSnapshot = image.RtspUrlSnapshot,
                ImageFormat = image.ImageFormat,
                ImageBytes = image.ImageBytes,
                FileSizeBytes = image.FileSizeBytes,
                CapturedAt = image.CapturedAt,
                CapturedBy = image.CapturedBy,
                CreatedAt = image.CreatedAt,
                CreatedBy = image.CreatedBy,
                UpdatedAt = image.UpdatedAt,
                UpdatedBy = image.UpdatedBy
            });

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return new SyncWeighTicketResponse { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = response.StatusCode.ToString(),
                ErrorMessage = body
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SyncWeighTicketResponse { Success = false, ErrorCode = "TIMEOUT", ErrorMessage = "Request timed out" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to push weighing session image {ImageId}.", image.Id);
            return new SyncWeighTicketResponse { Success = false, ErrorCode = "NETWORK_ERROR", ErrorMessage = ex.Message };
        }
    }

    private async Task<SyncEndpointRoute> ResolveRouteAsync(string? stationCode, CancellationToken ct)
    {
        var resolver = new BackupSyncRouteResolver(_scopeFactory, _logger);
        return await resolver.ResolveForImageAsync(stationCode, _httpClient.BaseAddress, ct);
    }
}
