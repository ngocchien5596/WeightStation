using Microsoft.Extensions.Options;
using StationApp.CentralApi.Configuration;

namespace StationApp.CentralApi.Authentication;

public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<CentralApiOptions> _options;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<CentralApiOptions> options,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var configuredKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogWarning(
                "API key authentication is disabled because CentralApi:ApiKey is empty. Path={Path} TraceId={TraceId}",
                context.Request.Path,
                context.TraceIdentifier);
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) ||
            !string.Equals(apiKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Unauthorized request rejected. Method={Method} Path={Path} RemoteIp={RemoteIp} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Invalid API key." });
            return;
        }

        await _next(context);
    }
}
