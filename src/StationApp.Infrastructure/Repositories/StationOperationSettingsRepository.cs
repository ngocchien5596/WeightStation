using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public sealed class StationOperationSettingsRepository : IStationOperationSettingsRepository
{
    private readonly StationDbContext _context;

    public StationOperationSettingsRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string stationCode, string settingKey, CancellationToken ct)
    {
        return await _context.StationOperationSettings.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.SettingKey == settingKey)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsByStationAsync(string stationCode, CancellationToken ct)
    {
        var list = await _context.StationOperationSettings.AsNoTracking()
            .Where(x => x.StationCode == stationCode)
            .ToListAsync(ct);
        return list.ToDictionary(x => x.SettingKey, x => x.SettingValue);
    }

    public async Task SaveSettingsAsync(string stationCode, IReadOnlyDictionary<string, string> settings, string actor, CancellationToken ct)
    {
        var existingSettings = await _context.StationOperationSettings
            .Where(x => x.StationCode == stationCode)
            .ToListAsync(ct);

        var now = DateTime.Now;

        foreach (var kvp in settings)
        {
            var existing = existingSettings.FirstOrDefault(x => x.SettingKey == kvp.Key);
            if (existing != null)
            {
                if (existing.SettingValue != kvp.Value)
                {
                    existing.SettingValue = kvp.Value;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = actor;
                }
            }
            else
            {
                var newSetting = new StationOperationSetting
                {
                    Id = Guid.NewGuid(),
                    StationCode = stationCode,
                    SettingKey = kvp.Key,
                    SettingValue = kvp.Value,
                    CreatedAt = now,
                    CreatedBy = actor
                };
                await _context.StationOperationSettings.AddAsync(newSetting, ct);
            }
        }
    }
}
