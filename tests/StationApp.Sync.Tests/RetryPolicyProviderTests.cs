using Xunit;
using StationApp.Sync.Services;

namespace StationApp.Sync.Tests;

public class RetryPolicyProviderTests
{
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 120)]
    [InlineData(2, 600)]
    [InlineData(3, 1800)]
    [InlineData(4, 7200)]
    public void GetNextRetryAt_Returns_Correct_Delay(int retryCount, int expectedSeconds)
    {
        var now = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);
        var result = RetryPolicyProvider.GetNextRetryAt(retryCount, now);
        Assert.Equal(now.AddSeconds(expectedSeconds), result);
    }

    [Fact]
    public void IsMaxRetryReached_True_After_5_Retries()
    {
        Assert.True(RetryPolicyProvider.IsMaxRetryReached(5));
    }

    [Fact]
    public void IsMaxRetryReached_False_Before_Max()
    {
        Assert.False(RetryPolicyProvider.IsMaxRetryReached(3));
    }
}
