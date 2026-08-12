using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Sync.Services;
using Xunit;

namespace StationApp.Sync.Tests;

public sealed class CentralApiClientRoutingTests
{
    [Fact]
    public async Task PushAggregateAsync_UsesCentralApi_ForQn01Payload()
    {
        var handler = new CapturingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fallback-central/")
        };
        var config = CreateConfig(new Dictionary<string, string?>
        {
            [AppConfigKeys.CentralApiUrl] = "http://central-api/",
            [AppConfigKeys.CentralApiKey] = "central-key",
            [AppConfigKeys.BackupSyncApiUrl] = "http://backup-api/",
            [AppConfigKeys.BackupSyncApiKey] = "backup-key",
            [AppConfigKeys.BackupSyncEnabled] = "true",
            [AppConfigKeys.BackupSyncStationCodes] = "QN02,QN03"
        });
        var client = new CentralApiClient(http, CreateScopeFactory(config), NullLogger<CentralApiClient>.Instance);

        var result = await client.PushAggregateAsync(
            SyncAggregateTypes.WeighingSession,
            "{\"StationCode\":\"QN01\",\"Id\":\"00000000-0000-0000-0000-000000000001\"}",
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("http://central-api/api/weighing-sessions", handler.LastRequestUri?.AbsoluteUri);
        Assert.Equal("central-key", handler.LastApiKey);
    }

    [Fact]
    public async Task PushAggregateAsync_UsesBackupSync_ForQn02Payload()
    {
        var handler = new CapturingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fallback-central/")
        };
        var config = CreateConfig(new Dictionary<string, string?>
        {
            [AppConfigKeys.CentralApiUrl] = "http://central-api/",
            [AppConfigKeys.CentralApiKey] = "central-key",
            [AppConfigKeys.BackupSyncApiUrl] = "http://backup-api/",
            [AppConfigKeys.BackupSyncApiKey] = "backup-key",
            [AppConfigKeys.BackupSyncEnabled] = "true",
            [AppConfigKeys.BackupSyncStationCodes] = "QN02,QN03"
        });
        var client = new CentralApiClient(http, CreateScopeFactory(config), NullLogger<CentralApiClient>.Instance);

        var result = await client.PushAggregateAsync(
            SyncAggregateTypes.WeighingSession,
            "{\"StationCode\":\"QN02\",\"Id\":\"00000000-0000-0000-0000-000000000002\"}",
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("http://backup-api/api/weighing-sessions", handler.LastRequestUri?.AbsoluteUri);
        Assert.Equal("backup-key", handler.LastApiKey);
    }

    [Fact]
    public async Task PushAggregateAsync_DoesNotFallbackToCentral_WhenBackupStationHasNoBackupUrl()
    {
        var handler = new CapturingHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fallback-central/")
        };
        var config = CreateConfig(new Dictionary<string, string?>
        {
            [AppConfigKeys.CentralApiUrl] = "http://central-api/",
            [AppConfigKeys.CentralApiKey] = "central-key",
            [AppConfigKeys.BackupSyncApiUrl] = "",
            [AppConfigKeys.BackupSyncApiKey] = "backup-key",
            [AppConfigKeys.BackupSyncEnabled] = "true",
            [AppConfigKeys.BackupSyncStationCodes] = "QN02,QN03"
        });
        var client = new CentralApiClient(http, CreateScopeFactory(config), NullLogger<CentralApiClient>.Instance);

        var result = await client.PushAggregateAsync(
            SyncAggregateTypes.WeighingSession,
            "{\"StationCode\":\"QN03\",\"Id\":\"00000000-0000-0000-0000-000000000003\"}",
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("CONFIG_INVALID", result.ErrorCode);
        Assert.Null(handler.LastRequestUri);
    }

    private static IAppConfigRepository CreateConfig(IReadOnlyDictionary<string, string?> values)
    {
        var config = Substitute.For<IAppConfigRepository>();
        config.GetValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(values.GetValueOrDefault(call.ArgAt<string>(0))));
        return config;
    }

    private static IServiceScopeFactory CreateScopeFactory(IAppConfigRepository config)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(IAppConfigRepository)).Returns(config);
        return scopeFactory;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastApiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? values.SingleOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
