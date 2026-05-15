using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;

namespace StationApp.Sync.Services;

public interface ICentralApiClient
{
    Task<SyncWeighTicketResponse> PushAggregateAsync(string aggregateType, string payloadJson, Guid idempotencyKey, CancellationToken ct);
    Task<SyncWeighTicketResponse> PushTicketAsync(string payloadJson, Guid idempotencyKey, CancellationToken ct);
    Task<InboundMasterDataResponse> PullMasterDataAsync(DateTime? lastSyncAt, CancellationToken ct);
}

public sealed class CentralApiClient : ICentralApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<CentralApiClient>? _logger;

    public CentralApiClient(HttpClient httpClient, IServiceScopeFactory? scopeFactory = null, ILogger<CentralApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Pushes a sync aggregate to Central API (Outbound).
    /// X-Api-Key header is set via HttpClient message handler in DI config.
    /// </summary>
    public async Task<SyncWeighTicketResponse> PushAggregateAsync(string aggregateType, string payloadJson, Guid idempotencyKey, CancellationToken ct)
    {
        try
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

            var endpoint = ResolveOutboundEndpoint(aggregateType);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, endpoint))
            {
                Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());

            _logger?.LogDebug(
                "Pushing aggregate {AggregateType} to Central API endpoint {Endpoint}. Idempotency-Key: {Key}",
                aggregateType,
                endpoint,
                idempotencyKey);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation(
                    "Aggregate {AggregateType} pushed successfully. Idempotency-Key: {Key}",
                    aggregateType,
                    idempotencyKey);
                return new SyncWeighTicketResponse { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning(
                "Push aggregate {AggregateType} failed. Status: {Status}, Body: {Body}",
                aggregateType,
                response.StatusCode,
                body);
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = response.StatusCode.ToString(),
                ErrorMessage = body
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error pushing aggregate {AggregateType}. Idempotency-Key: {Key}", aggregateType, idempotencyKey);
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                ErrorMessage = ex.Message
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger?.LogError(ex, "Timeout pushing aggregate {AggregateType}. Idempotency-Key: {Key}", aggregateType, idempotencyKey);
            return new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = "TIMEOUT",
                ErrorMessage = "Request timed out"
            };
        }
    }

    public Task<SyncWeighTicketResponse> PushTicketAsync(string payloadJson, Guid idempotencyKey, CancellationToken ct)
        => PushAggregateAsync(SyncAggregateTypes.WeighTicket, payloadJson, idempotencyKey, ct);

    /// <summary>
    /// Pulls latest master data from Central API (Inbound).
    /// </summary>
    public async Task<InboundMasterDataResponse> PullMasterDataAsync(DateTime? lastSyncAt, CancellationToken ct)
    {
        try
        {
            var baseUri = await ResolveBaseUriAsync(ct);
            if (baseUri == null)
            {
                return new InboundMasterDataResponse
                {
                    Success = false,
                    ErrorMessage = "Central API URL chua duoc cau hinh hop le."
                };
            }

            var url = "api/master-data";
            if (lastSyncAt.HasValue)
                url += $"?since={lastSyncAt.Value:O}";

            _logger?.LogDebug("Pulling master data from Central API. Since: {Since}", lastSyncAt);

            var response = await _httpClient.GetAsync(new Uri(baseUri, url), ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<InboundMasterDataResponse>(ct);
                if (data != null)
                {
                    data.Success = true;
                    _logger?.LogInformation(
                        "Master data pulled: {Vehicles} vehicles, {Customers} customers, {Products} products",
                        data.Vehicles.Count, data.Customers.Count, data.Products.Count);
                    return data;
                }
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning("Pull master data failed. Status: {Status}", response.StatusCode);
            return new InboundMasterDataResponse
            {
                Success = false,
                ErrorMessage = $"HTTP {response.StatusCode}: {body}"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error pulling master data");
            return new InboundMasterDataResponse
            {
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger?.LogError(ex, "Timeout pulling master data");
            return new InboundMasterDataResponse
            {
                Success = false,
                ErrorMessage = "Request timed out"
            };
        }
    }

    private async Task<Uri?> ResolveBaseUriAsync(CancellationToken ct)
    {
        var configuredUrl = await TryGetConfiguredBaseUrlAsync(ct);
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
            {
                _logger?.LogWarning("Central API URL is invalid: {Url}", configuredUrl);
                return null;
            }

            if (configuredUri.IsLoopback)
            {
                _logger?.LogWarning("Central API URL points to loopback and is ignored on station clients: {Url}", configuredUrl);
                return null;
            }

            return EnsureTrailingSlash(configuredUri);
        }

        return _httpClient.BaseAddress is null ? null : EnsureTrailingSlash(_httpClient.BaseAddress);
    }

    private async Task<string?> TryGetConfiguredBaseUrlAsync(CancellationToken ct)
    {
        if (_scopeFactory == null)
        {
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            return await config.GetValueAsync("central_api_url", ct);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to read central_api_url from app config.");
            return null;
        }
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/");
    }

    private static string ResolveOutboundEndpoint(string aggregateType)
    {
        return aggregateType switch
        {
            SyncAggregateTypes.VehicleRegistration => "api/vehicle-registrations",
            SyncAggregateTypes.WeighTicket => "api/weigh-tickets",
            SyncAggregateTypes.DeliveryTicket => "api/delivery-tickets",
            _ => throw new InvalidOperationException($"Unsupported sync aggregate type: {aggregateType}")
        };
    }
}
