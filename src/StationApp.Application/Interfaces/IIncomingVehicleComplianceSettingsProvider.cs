using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IIncomingVehicleComplianceSettingsProvider
{
    Task<IncomingVehicleComplianceRules> GetCurrentRulesAsync(CancellationToken ct);
}
