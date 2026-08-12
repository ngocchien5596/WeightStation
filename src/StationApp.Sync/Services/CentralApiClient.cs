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

    public async Task<SyncWeighTicketResponse> PushAggregateAsync(string aggregateType, string payloadJson, Guid idempotencyKey, CancellationToken ct)
    {
        try
        {
            var route = await ResolveRouteAsync(payloadJson, ct);
            if (route.BaseUri == null)
            {
                return new SyncWeighTicketResponse
                {
                    Success = false,
                    ErrorCode = "CONFIG_INVALID",
                    ErrorMessage = route.ErrorMessage ?? "Sync API URL chưa được cấu hình hợp lệ."
                };
            }

            var endpoint = ResolveOutboundEndpoint(aggregateType);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route.BaseUri, endpoint))
            {
                Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
            if (!string.IsNullOrWhiteSpace(route.ApiKey))
            {
                request.Headers.Remove("X-Api-Key");
                request.Headers.Add("X-Api-Key", route.ApiKey);
            }

            _logger?.LogDebug(
                "Pushing aggregate {AggregateType} to {SyncChannel} endpoint {Endpoint}. StationCode={StationCode}. Idempotency-Key: {Key}",
                aggregateType,
                route.Channel,
                endpoint,
                route.StationCode ?? "-",
                idempotencyKey);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger?.LogInformation(
                    "Aggregate {AggregateType} pushed successfully to {SyncChannel}. StationCode={StationCode}. Idempotency-Key: {Key}",
                    aggregateType,
                    route.Channel,
                    route.StationCode ?? "-",
                    idempotencyKey);
                return new SyncWeighTicketResponse { Success = true };
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger?.LogWarning(
                "Push aggregate {AggregateType} to {SyncChannel} failed. StationCode={StationCode}. Status: {Status}, Body: {Body}",
                aggregateType,
                route.Channel,
                route.StationCode ?? "-",
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

    private async Task<SyncEndpointRoute> ResolveRouteAsync(string payloadJson, CancellationToken ct)
    {
        if (_scopeFactory == null)
        {
            return new SyncEndpointRoute(
                _httpClient.BaseAddress is null ? null : EnsureTrailingSlash(_httpClient.BaseAddress),
                null,
                "CENTRAL",
                null,
                _httpClient.BaseAddress is null ? "Central API URL chưa được cấu hình hợp lệ." : null);
        }

        var resolver = new BackupSyncRouteResolver(_scopeFactory, _logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CentralApiClient>.Instance);
        return await resolver.ResolveForPayloadAsync(payloadJson, _httpClient.BaseAddress, ct);
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
            SyncAggregateTypes.CutOrder => "api/vehicle-registrations",
            SyncAggregateTypes.WeighTicket => "api/weigh-tickets",
            SyncAggregateTypes.DeliveryTicket => "api/delivery-tickets",
            SyncAggregateTypes.WeighingSession => "api/weighing-sessions",
            SyncAggregateTypes.WeighingSessionLine => "api/weighing-session-lines",
            SyncAggregateTypes.Station => "api/stations",
            SyncAggregateTypes.Vehicle => "api/vehicles",
            SyncAggregateTypes.Customer => "api/customers",
            SyncAggregateTypes.Product => "api/products",
            SyncAggregateTypes.IncomingSeedVehicle => "api/incoming-seed-vehicles",
            SyncAggregateTypes.AuditLog => "api/audit-logs",
            SyncAggregateTypes.User => "api/users",
            SyncAggregateTypes.UserStationAssignment => "api/user-station-assignments",
            SyncAggregateTypes.PrintTemplateProfile => "api/print-template-profiles",
            _ => throw new InvalidOperationException($"Unsupported sync aggregate type: {aggregateType}")
        };
    }
}
