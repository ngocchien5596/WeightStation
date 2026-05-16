using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;

namespace StationApp.Sync.Services;

/// <summary>
/// Background worker that periodically pulls master data (Vehicles, Customers, Products)
/// from the Central API and upserts them into the local database.
/// This is the Inbound flow as defined in Section 5.1 of the Phase 2 spec.
/// </summary>
public sealed class InboundMasterDataWorker : BackgroundService
{
    private const int FailureThresholdBeforeCooldown = 3;
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboundMasterDataWorker> _logger;
    private readonly IClock _clock;
    private int _intervalSeconds = 300; // Default: every 5 minutes
    private int _consecutiveFailures;
    private DateTime? _cooldownUntilLocal;

    public InboundMasterDataWorker(IServiceScopeFactory scopeFactory, ILogger<InboundMasterDataWorker> logger, IClock clock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InboundMasterDataWorker started. Interval: {Interval}s", _intervalSeconds);

        // Wait a bit before first pull to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_cooldownUntilLocal.HasValue && _cooldownUntilLocal.Value > _clock.NowLocal)
                {
                    await ReportStatusAsync(
                        "Cooldown",
                        $"Tạm dừng đồng bộ master-data đến {_cooldownUntilLocal.Value:O} sau {_consecutiveFailures} lần thất bại liên tiếp.",
                        stoppingToken);
                }
                else
                {
                    await PullAndUpsertAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in InboundMasterDataWorker cycle");
                await RegisterFailureAsync($"Unhandled worker error: {ex.Message}", stoppingToken);
            }

            // Read interval from config for each cycle (can be changed at runtime)
            await RefreshIntervalAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    private async Task PullAndUpsertAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var apiClient = scope.ServiceProvider.GetRequiredService<ICentralApiClient>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Read last sync timestamp
        var lastSyncStr = await appConfig.GetValueAsync("master_data_last_sync", ct);
        DateTime? lastSyncAt = null;
        if (DateTime.TryParse(lastSyncStr, out var parsed))
            lastSyncAt = parsed;

        var response = await apiClient.PullMasterDataAsync(lastSyncAt, ct);
        if (!response.Success)
        {
            _logger.LogWarning("Master data pull failed: {Error}", response.ErrorMessage);
            await RegisterFailureAsync(response.ErrorMessage ?? "Master data pull failed", ct);
            return;
        }

        var now = clock.NowLocal;

        // Upsert Vehicles
        foreach (var dto in response.Vehicles)
        {
            var existing = await vehicleRepo.GetByPlateAndMoocAsync(dto.VehiclePlate, dto.MoocNumber, ct);
            if (existing == null)
            {
                existing = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    VehiclePlate = dto.VehiclePlate,
                    MoocNumber = dto.MoocNumber,
                    CreatedAt = now,
                    CreatedBy = "SYNC"
                };
                await vehicleRepo.AddAsync(existing, ct);
            }

            if (!string.IsNullOrEmpty(dto.DriverName)) existing.DriverName = dto.DriverName;
            if (!string.IsNullOrEmpty(dto.TransportMethod)) existing.TransportMethod = dto.TransportMethod;
            if (dto.TtcpWeight.HasValue) existing.TtcpWeight = dto.TtcpWeight;
            if (!string.IsNullOrEmpty(dto.VehicleRegistrationNo)) existing.VehicleRegistrationNo = dto.VehicleRegistrationNo;
            if (dto.VehicleRegistrationExpiryDate.HasValue) existing.VehicleRegistrationExpiryDate = dto.VehicleRegistrationExpiryDate;
            if (!string.IsNullOrEmpty(dto.MoocRegistrationNo)) existing.MoocRegistrationNo = dto.MoocRegistrationNo;
            if (dto.MoocRegistrationExpiryDate.HasValue) existing.MoocRegistrationExpiryDate = dto.MoocRegistrationExpiryDate;
            existing.UpdatedAt = now;
            existing.UpdatedBy = "SYNC";
        }

        // Upsert Customers
        foreach (var dto in response.Customers)
        {
            var existing = await customerRepo.GetByCodeAsync(dto.CustomerCode, ct);
            if (existing == null)
            {
                existing = new Customer
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = dto.CustomerCode,
                    CustomerName = dto.CustomerName,
                    CreatedAt = now,
                    CreatedBy = "SYNC"
                };
                await customerRepo.AddAsync(existing, ct);
            }
            else
            {
                if (!string.IsNullOrEmpty(dto.CustomerName)) existing.CustomerName = dto.CustomerName;
                existing.UpdatedAt = now;
                existing.UpdatedBy = "SYNC";
            }
        }

        // Upsert Products
        foreach (var dto in response.Products)
        {
            var existing = await productRepo.GetByCodeAsync(dto.ProductCode, ct);
            if (existing == null)
            {
                existing = new Product
                {
                    Id = Guid.NewGuid(),
                    ProductCode = dto.ProductCode,
                    ProductName = dto.ProductName,
                    CreatedAt = now,
                    CreatedBy = "SYNC"
                };
                await productRepo.AddAsync(existing, ct);
            }
            else
            {
                if (!string.IsNullOrEmpty(dto.ProductName)) existing.ProductName = dto.ProductName;
                existing.UpdatedAt = now;
                existing.UpdatedBy = "SYNC";
            }
        }

        await uow.SaveChangesAsync(ct);

        // Update last sync timestamp
        await appConfig.SetValueAsync("master_data_last_sync", now.ToString("O"), ct);
        await appConfig.SetValueAsync("master_data_sync_status", "Healthy", ct);
        await appConfig.SetValueAsync("master_data_sync_error", string.Empty, ct);
        await uow.SaveChangesAsync(ct);
        _consecutiveFailures = 0;
        _cooldownUntilLocal = null;

        _logger.LogInformation(
            "Master data sync completed. Vehicles: {V}, Customers: {C}, Products: {P}",
            response.Vehicles.Count, response.Customers.Count, response.Products.Count);
    }

    private async Task RefreshIntervalAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var val = await appConfig.GetValueAsync("sync_interval", ct);
            if (int.TryParse(val, out var interval) && interval >= 30)
                _intervalSeconds = interval;
        }
        catch { }
    }

    private async Task RegisterFailureAsync(string errorMessage, CancellationToken ct)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= FailureThresholdBeforeCooldown)
        {
            _cooldownUntilLocal = _clock.NowLocal.Add(FailureCooldown);
        }

        var status = _cooldownUntilLocal.HasValue && _cooldownUntilLocal.Value > _clock.NowLocal
            ? "Cooldown"
            : "Degraded";

        await ReportStatusAsync(status, errorMessage, ct);
    }

    private async Task ReportStatusAsync(string status, string message, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await appConfig.SetValueAsync("master_data_sync_status", status, ct);
            await appConfig.SetValueAsync("master_data_sync_error", message, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist master-data sync status.");
        }
    }
}
