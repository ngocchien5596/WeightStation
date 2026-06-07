using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface ICutOrderRepository
{
    Task AddAsync(CutOrder cutOrder, CancellationToken ct);
    Task UpdateAsync(CutOrder cutOrder, CancellationToken ct);
    Task<CutOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<CutOrder?> GetByErpIdAsync(string erpCutOrderId, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetLatestDeletedByErpIdsAsync(IReadOnlyCollection<string> erpCutOrderIds, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetLatestDeletedByRegistrationCodesAsync(IReadOnlyCollection<string> erpRegistrationCodes, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> SearchAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<CutOrder>> GetUnprocessedInboundAsync(CancellationToken ct);
    Task<IReadOnlyList<WeightViewListItem>> GetWeightViewListAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<IncomingVehicleListItem>> GetIncomingListAsync(IncomingVehicleListFilter filter, CancellationToken ct);
    Task<IReadOnlyList<OutgoingVehicleListItem>> GetOutgoingListAsync(OutgoingVehicleListFilter filter, CancellationToken ct);
    Task<IReadOnlyList<ExportScaleCutOrderListItem>> GetActiveExportScaleCutOrdersAsync(ExportScaleCutOrderFilter filter, CancellationToken ct);
    Task<IReadOnlyList<TemporaryExportCutOrderOption>> GetActiveTemporaryExportCutOrderOptionsAsync(Guid? realCutOrderId, CancellationToken ct);
    Task<string> GenerateTemporaryExportDisplayCodeAsync(CancellationToken ct);
    Task<IReadOnlyList<ExportVehicleTripListItem>> GetExportVehicleTripsAsync(Guid cutOrderId, CancellationToken ct);
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchMoocHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<DriverAutocompleteSource>> SearchDriverHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<CustomerAutocompleteSource>> SearchCustomerHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductCodeHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductNameHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
}

