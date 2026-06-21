using StationApp.Application.Formatting;
using Xunit;

namespace StationApp.Application.Tests;

public class ExportReturnedBrokenTripHelperTests
{
    [Fact]
    public void ResolveSignedBagCount_PrefersBagCountDisplay_WhenAvailable()
    {
        var normalValue = ExportReturnedBrokenTripHelper.ResolveSignedBagCount(
            actualAllocatedBagCount: 10000,
            bagCountDisplay: 20,
            weightKg: 1000m,
            bagWeightKg: 50m,
            isReturnedBrokenTrip: false);
        var returnedValue = ExportReturnedBrokenTripHelper.ResolveSignedBagCount(
            actualAllocatedBagCount: 10000,
            bagCountDisplay: 20,
            weightKg: 1000m,
            bagWeightKg: 50m,
            isReturnedBrokenTrip: true);

        Assert.Equal(20, normalValue);
        Assert.Equal(-20, returnedValue);
    }
}
