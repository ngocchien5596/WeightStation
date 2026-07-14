namespace StationApp.Application.Services;

public sealed record ReturnedBrokenTripWeightResolution(
    decimal ActualWeightKg,
    decimal? PreviousTripWeightKg,
    decimal RecognizedWeightKg,
    bool HasPreviousTrip,
    bool IsCapped)
{
    public decimal ActualWeightTon => ActualWeightKg / 1000m;
    public decimal? PreviousTripWeightTon => PreviousTripWeightKg / 1000m;
    public decimal RecognizedWeightTon => RecognizedWeightKg / 1000m;
}

public static class ReturnedBrokenTripWeightLimiter
{
    public static ReturnedBrokenTripWeightResolution Resolve(decimal actualWeightKg, decimal? previousTripWeightKg)
    {
        var normalizedActual = Math.Max(0m, actualWeightKg);
        if (!previousTripWeightKg.HasValue || previousTripWeightKg.Value <= 0m)
        {
            return new ReturnedBrokenTripWeightResolution(
                normalizedActual,
                null,
                normalizedActual,
                false,
                false);
        }

        var normalizedPrevious = Math.Max(0m, previousTripWeightKg.Value);
        var recognized = Math.Min(normalizedActual, normalizedPrevious);
        return new ReturnedBrokenTripWeightResolution(
            normalizedActual,
            normalizedPrevious,
            recognized,
            true,
            normalizedActual > normalizedPrevious);
    }
}
