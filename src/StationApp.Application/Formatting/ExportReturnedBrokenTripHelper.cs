namespace StationApp.Application.Formatting;

public static class ExportReturnedBrokenTripHelper
{
    public static decimal ResolveSignedWeight(decimal? weightKg, bool isReturnedBrokenTrip)
    {
        var value = weightKg ?? 0m;
        return isReturnedBrokenTrip ? -value : value;
    }

    public static int ResolveSignedBagCount(
        int? actualAllocatedBagCount,
        int? bagCountDisplay,
        decimal? weightKg,
        decimal? bagWeightKg,
        bool isReturnedBrokenTrip)
    {
        // Keep export progress aligned with the bag count shown in the trip grid.
        var value = bagCountDisplay
            ?? actualAllocatedBagCount
            ?? BagCountDisplayHelper.Resolve(weightKg, bagWeightKg, null)
            ?? 0;

        return isReturnedBrokenTrip ? -value : value;
    }
}
