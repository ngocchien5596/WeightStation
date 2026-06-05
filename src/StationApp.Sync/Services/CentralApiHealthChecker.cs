using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;

namespace StationApp.Sync.Services;

public sealed class CentralApiHealthChecker : ICentralApiHealthChecker
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public CentralApiHealthChecker(HttpClient httpClient, IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
    }

    public async Task<CentralApiHealthCheckResult> CheckAsync(CancellationToken ct)
    {
        var baseUri = await ResolveBaseUriAsync(null, ct);
        return await CheckInternalAsync(baseUri, null, ct);
    }

    public async Task<CentralApiHealthCheckResult> CheckAsync(string? baseUrl, string? apiKey, CancellationToken ct)
    {
        var baseUri = await ResolveBaseUriAsync(baseUrl, ct);
        return await CheckInternalAsync(baseUri, apiKey, ct);
    }

    private async Task<CentralApiHealthCheckResult> CheckInternalAsync(Uri? baseUri, string? apiKey, CancellationToken ct)
    {
        if (baseUri == null)
        {
            return new CentralApiHealthCheckResult(false, "CONFIG_INVALID", "Central API URL chưa được cấu hình hợp lệ.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "health"));
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Remove("X-Api-Key");
                request.Headers.Add("X-Api-Key", apiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return new CentralApiHealthCheckResult(true, "OK", "Kết nối Central API thành công.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return new CentralApiHealthCheckResult(false, ((int)response.StatusCode).ToString(), string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Health check failed." : body);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new CentralApiHealthCheckResult(false, "TIMEOUT", "Kết nối Central API bị timeout.");
        }
        catch (HttpRequestException ex)
        {
            return new CentralApiHealthCheckResult(false, "NETWORK_ERROR", ex.Message);
        }
    }

    private async Task<Uri?> ResolveBaseUriAsync(string? overrideBaseUrl, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(overrideBaseUrl))
        {
            return Uri.TryCreate(overrideBaseUrl.Trim(), UriKind.Absolute, out var overrideUri)
                ? EnsureTrailingSlash(overrideUri)
                : null;
        }

        using var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var configuredUrl = await config.GetValueAsync(AppConfigKeys.CentralApiUrl, ct)
            ?? await config.GetValueAsync("central_api_url", ct);

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return _httpClient.BaseAddress;
        }

        return Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            ? EnsureTrailingSlash(configuredUri)
            : null;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/");
    }
}
