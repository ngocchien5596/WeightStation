using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Infrastructure.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public class ToleranceProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    public async Task GetToleranceKgPerBagAsync_MissingOrInvalidConfig_ReturnsDefault(string? configuredValue)
    {
        var configRepo = new StubAppConfigRepository(configuredValue);
        var sut = new ToleranceProvider(configRepo);

        var tolerance = await sut.GetToleranceKgPerBagAsync(CancellationToken.None);

        Assert.Equal(AppConfigDefaults.DefaultToleranceKgPerBag, tolerance);
    }

    [Fact]
    public async Task GetToleranceKgPerBagAsync_ParsesInvariantDecimal()
    {
        var configRepo = new StubAppConfigRepository("1.75");
        var sut = new ToleranceProvider(configRepo);

        var tolerance = await sut.GetToleranceKgPerBagAsync(CancellationToken.None);

        Assert.Equal(1.75m, tolerance);
    }

    private sealed class StubAppConfigRepository : IAppConfigRepository
    {
        private readonly string? _value;

        public StubAppConfigRepository(string? value)
        {
            _value = value;
        }

        public Task<string?> GetValueAsync(string key, CancellationToken ct)
            => Task.FromResult(_value);

        public Task SetValueAsync(string key, string value, CancellationToken ct)
            => Task.CompletedTask;
    }
}
