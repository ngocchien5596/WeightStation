using StationApp.Domain.Entities;

namespace StationApp.Domain.Services;

public static class StandardTarePolicy
{
    public static decimal? GetEffectiveStandardTare(Vehicle? vehicle, DateTime todayLocal)
        => vehicle == null
            ? null
            : GetEffectiveStandardTare(vehicle.TtcpWeight, vehicle.StandardTareUpdatedAt, todayLocal);

    public static decimal? GetEffectiveStandardTare(decimal? tareWeight, DateTime? updatedAt, DateTime todayLocal)
    {
        if (!tareWeight.HasValue || tareWeight.Value <= 0)
        {
            return null;
        }

        if (!updatedAt.HasValue || updatedAt.Value.Date != todayLocal.Date)
        {
            return null;
        }

        return tareWeight.Value;
    }
}
