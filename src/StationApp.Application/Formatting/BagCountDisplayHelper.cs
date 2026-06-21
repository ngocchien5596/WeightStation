namespace StationApp.Application.Formatting;

public static class BagCountDisplayHelper
{
    public static int? Resolve(decimal? weightKg, decimal? bagWeightKg, int? fallback = null)
    {
        if (bagWeightKg.HasValue && bagWeightKg.Value > 0m && weightKg.HasValue)
        {
            return (int)decimal.Round(
                weightKg.Value / bagWeightKg.Value,
                0,
                MidpointRounding.AwayFromZero);
        }

        return fallback;
    }
}
