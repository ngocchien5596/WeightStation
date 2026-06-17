using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Constants;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Services;

namespace StationApp.Infrastructure.Repositories;

internal static class StationScopeQuery
{
    public static async Task<string> GetCurrentStationCodeAsync(StationDbContext db, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(StationRuntimeScope.StationCode))
        {
            return StationRuntimeScope.StationCode!;
        }

        var value = await db.AppConfigs.AsNoTracking()
            .Where(x => x.ConfigKey == AppConfigKeys.DefaultStationCode)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        value = await db.AppConfigs.AsNoTracking()
            .Where(x => x.ConfigKey == AppConfigKeys.StationCode)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(value) ? "QN01" : value.Trim();
    }
}
