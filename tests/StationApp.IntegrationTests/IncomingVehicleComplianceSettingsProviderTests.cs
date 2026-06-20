using StationApp.Application.Interfaces;
using StationApp.Infrastructure.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public class IncomingVehicleComplianceSettingsProviderTests
{
    [Fact]
    public async Task GetCurrentRulesAsync_ReturnsDisabledRules_ForStationsOtherThanQn01()
    {
        var stationScope = new FakeStationScope("QN02");
        var repository = new FakeStationOperationSettingsRepository(new Dictionary<string, string>
        {
            ["incoming_require_ttcp_for_bagged_outbound"] = "true",
            ["incoming_require_registration_for_bagged_outbound"] = "true"
        });

        var sut = new IncomingVehicleComplianceSettingsProvider(stationScope, repository);

        var result = await sut.GetCurrentRulesAsync(CancellationToken.None);

        Assert.False(result.BaggedOutbound.RequireTtcpOnCreateSession);
        Assert.False(result.BaggedOutbound.RequireRegistrationOnCreateSession);
        Assert.False(result.BulkOutbound.RequireTtcpOnCreateSession);
        Assert.False(result.BulkOutbound.RequireRegistrationOnCreateSession);
    }

    [Fact]
    public async Task GetCurrentRulesAsync_ReadsConfiguredRules_ForQn01()
    {
        var stationScope = new FakeStationScope("QN01");
        var repository = new FakeStationOperationSettingsRepository(new Dictionary<string, string>
        {
            ["incoming_require_ttcp_for_bagged_outbound"] = "true",
            ["incoming_require_registration_for_bagged_outbound"] = "false",
            ["incoming_require_ttcp_for_bulk_outbound"] = "false",
            ["incoming_require_registration_for_bulk_outbound"] = "true"
        });

        var sut = new IncomingVehicleComplianceSettingsProvider(stationScope, repository);

        var result = await sut.GetCurrentRulesAsync(CancellationToken.None);

        Assert.True(result.BaggedOutbound.RequireTtcpOnCreateSession);
        Assert.False(result.BaggedOutbound.RequireRegistrationOnCreateSession);
        Assert.False(result.BulkOutbound.RequireTtcpOnCreateSession);
        Assert.True(result.BulkOutbound.RequireRegistrationOnCreateSession);
    }

    private sealed class FakeStationScope : IStationScope
    {
        private readonly string _stationCode;

        public FakeStationScope(string stationCode)
        {
            _stationCode = stationCode;
        }

        public Task<string> GetCurrentStationCodeAsync(CancellationToken ct)
            => Task.FromResult(_stationCode);
    }

    private sealed class FakeStationOperationSettingsRepository : IStationOperationSettingsRepository
    {
        private readonly IReadOnlyDictionary<string, string> _settings;

        public FakeStationOperationSettingsRepository(IReadOnlyDictionary<string, string> settings)
        {
            _settings = settings;
        }

        public Task<string?> GetValueAsync(string stationCode, string settingKey, CancellationToken ct)
            => Task.FromResult(_settings.TryGetValue(settingKey, out var value) ? value : null);

        public Task<IReadOnlyDictionary<string, string>> GetSettingsByStationAsync(string stationCode, CancellationToken ct)
            => Task.FromResult(_settings);

        public Task SaveSettingsAsync(string stationCode, IReadOnlyDictionary<string, string> settings, string actor, CancellationToken ct)
            => Task.CompletedTask;
    }
}
