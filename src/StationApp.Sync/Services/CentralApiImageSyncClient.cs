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
        var baseUri = await ResolveBaseUriAsync(ct);
        if (baseUri == null)
        {
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = "CONFIG_INVALID",
                ErrorMessage = "Central API URL chua duoc cau hinh hop le."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/weighing-session-images"));
            request.Headers.Add("Idempotency-Key", image.Id.ToString());
            request.Content = JsonContent.Create(new SyncWeighingSessionImageRequest
            {
                Id = image.Id,
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

    private async Task<Uri?> ResolveBaseUriAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var configuredUrl = await config.GetValueAsync(AppConfigKeys.CentralApiUrl, ct)
            ?? await config.GetValueAsync("central_api_url", ct);

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return _httpClient.BaseAddress;
        }

        return Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            ? (configuredUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? configuredUri : new Uri($"{configuredUri.AbsoluteUri}/"))
            : null;
    }
}
