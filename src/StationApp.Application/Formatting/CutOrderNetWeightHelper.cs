using StationApp.Domain.Constants;
using StationApp.Domain.Entities;

namespace StationApp.Application.Formatting;

public static class CutOrderNetWeightHelper
{
    public static decimal? ResolveDeliveryTicketActualWeightKg(
        CutOrder registration,
        decimal? actualWeightKg,
        bool useActualWeightForBaggedCutOrders)
    {
        if (registration.IsExportScale && registration.ExportFinalizedWeight.HasValue)
        {
            return registration.ExportFinalizedWeight;
        }

        if (!actualWeightKg.HasValue)
        {
            return null;
        }

        var normalizedProductType = ProductTypes.Normalize(registration.ProductType);
        var isBagged = string.Equals(normalizedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase)
            || registration.BagWeightKg.GetValueOrDefault() > 0m;

        if (!isBagged)
        {
            return actualWeightKg;
        }

        if (!useActualWeightForBaggedCutOrders)
        {
            return registration.PlannedWeight ?? 0m;
        }

        return RoundBaggedActualWeightKg(actualWeightKg.Value);
    }

    public static decimal RoundBaggedActualWeightKg(decimal actualWeightKg)
    {
        var roundedDownToHundreds = decimal.Floor(actualWeightKg / 100m) * 100m;
        var remainder = actualWeightKg % 100m;

        return roundedDownToHundreds
            + remainder switch
            {
                > 50m => 100m,
                50m => 50m,
                _ => 0m
            };
    }
}
