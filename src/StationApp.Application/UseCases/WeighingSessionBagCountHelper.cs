using System;
using StationApp.Domain.Constants;

namespace StationApp.Application.UseCases;

internal static class WeighingSessionBagCountHelper
{
    public static int? ResolveActualBagCount(
        string? productType,
        int? registrationBagCount,
        int? plannedBagCount,
        int? fallbackBagCount = null)
    {
        if (!string.Equals(ProductTypes.Normalize(productType), ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase))
        {
            return fallbackBagCount;
        }

        return registrationBagCount ?? plannedBagCount ?? fallbackBagCount;
    }
}
