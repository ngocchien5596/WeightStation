using StationApp.Domain.Services;
using Xunit;

namespace StationApp.Domain.Tests;

public class StandardTarePolicyTests
{
    [Fact]
    public void GetEffectiveStandardTare_ReturnsNull_WhenTareMissing()
    {
        var result = StandardTarePolicy.GetEffectiveStandardTare(null, new DateTime(2026, 6, 24), new DateTime(2026, 6, 24));

        Assert.Null(result);
    }

    [Fact]
    public void GetEffectiveStandardTare_ReturnsNull_WhenTareIsFromPreviousDay()
    {
        var result = StandardTarePolicy.GetEffectiveStandardTare(15000m, new DateTime(2026, 6, 23, 23, 0, 0), new DateTime(2026, 6, 24));

        Assert.Null(result);
    }

    [Fact]
    public void GetEffectiveStandardTare_ReturnsValue_WhenTareIsUpdatedToday()
    {
        var result = StandardTarePolicy.GetEffectiveStandardTare(15000m, new DateTime(2026, 6, 24, 7, 30, 0), new DateTime(2026, 6, 24));

        Assert.Equal(15000m, result);
    }
}
