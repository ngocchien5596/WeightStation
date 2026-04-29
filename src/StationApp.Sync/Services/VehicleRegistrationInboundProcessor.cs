using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Sync.Services;

/// <summary>
/// Background processor handling post-insert logic for VehicleRegistrations directly inserted by ERP.
/// </summary>
public class VehicleRegistrationInboundProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleRegistrationInboundProcessor> _logger;
    private int _pollSeconds = 5;

    public VehicleRegistrationInboundProcessor(IServiceScopeFactory scopeFactory, ILogger<VehicleRegistrationInboundProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VehicleRegistrationInboundProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var appRepo = scope.ServiceProvider.GetService<IAppConfigRepository>();
                    if (appRepo != null)
                    {
                        var pollStr = await appRepo.GetValueAsync("inbound_processor_poll_seconds", stoppingToken);
                        if (int.TryParse(pollStr, out var pollVal) && pollVal > 0)
                        {
                            _pollSeconds = pollVal;
                        }
                    }
                }
            }
            catch { }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await ProcessPendingInboundAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in VehicleRegistrationInboundProcessor execution cycle.");
            }
            sw.Stop();
            LogPerf("Inbound processor cycle", sw.Elapsed.TotalMilliseconds);

            await Task.Delay(TimeSpan.FromSeconds(_pollSeconds), stoppingToken);
        }
    }


    private async Task ProcessPendingInboundAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateFactoryScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        // Dynamic polling update
        var pollVal = await appConfig.GetValueAsync("registration_inbound_poll_seconds", ct);
        if (int.TryParse(pollVal, out var secs) && secs >= 1)
        {
            _pollSeconds = secs;
        }

        var unprocessed = await regRepo.GetUnprocessedInboundAsync(ct);
        if (unprocessed.Count == 0) return;

        foreach (var reg in unprocessed)
        {
            try
            {
                await ProcessSingleRegistrationAsync(reg, regRepo, vehicleRepo, customerRepo, productRepo, uow, audit, clock, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbound VehicleRegistration {Id}", reg.Id);
            }
        }
    }

    private async Task ProcessSingleRegistrationAsync(
        VehicleRegistration reg,
        IVehicleRegistrationRepository regRepo,
        IVehicleRepository vehicleRepo,
        ICustomerRepository customerRepo,
        IProductRepository productRepo,
        IUnitOfWork uow,
        IAuditService audit,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Normalize
        reg.VehiclePlate = (reg.VehiclePlate ?? string.Empty).Trim().ToUpper();
        reg.MoocNumber = (reg.MoocNumber ?? string.Empty).Trim();
        reg.CustomerCode = (reg.CustomerCode ?? string.Empty).Trim();
        reg.ProductCode = (reg.ProductCode ?? string.Empty).Trim();
        
        if (!string.IsNullOrWhiteSpace(reg.CustomerName)) reg.CustomerName = reg.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(reg.ProductName)) reg.ProductName = reg.ProductName.Trim();
        if (!string.IsNullOrWhiteSpace(reg.CutOrderCode)) reg.CutOrderCode = reg.CutOrderCode.Trim();
        if (!string.IsNullOrWhiteSpace(reg.OrderCode)) reg.OrderCode = reg.OrderCode.Trim();
        if (!string.IsNullOrWhiteSpace(reg.ReceiverName)) reg.ReceiverName = reg.ReceiverName.Trim();
        if (!string.IsNullOrWhiteSpace(reg.LotNo)) reg.LotNo = reg.LotNo.Trim();
        if (!string.IsNullOrWhiteSpace(reg.RepresentativeName)) reg.RepresentativeName = reg.RepresentativeName.Trim();
        if (!string.IsNullOrWhiteSpace(reg.ConsumptionPlace)) reg.ConsumptionPlace = reg.ConsumptionPlace.Trim();
        if (!string.IsNullOrWhiteSpace(reg.LoadingPlace)) reg.LoadingPlace = reg.LoadingPlace.Trim();
        if (!string.IsNullOrWhiteSpace(reg.SealNo)) reg.SealNo = reg.SealNo.Trim();

        // 2. Validate
        bool isValid = !string.IsNullOrEmpty(reg.VehiclePlate) &&
                       !string.IsNullOrEmpty(reg.CustomerCode) &&
                       !string.IsNullOrEmpty(reg.ProductCode);

        if (!isValid)
        {
            _logger.LogWarning("Validation failed for ERP Registration {Id}. Missing mandatory fields.", reg.Id);
            
            reg.InboundErrorCode = "VALIDATION_FAILED";
            reg.InboundErrorMessage = "Missing mandatory fields (vehicle_plate, customer_code, product_code).";
            reg.LastInboundAttemptAt = clock.NowLocal;
            reg.UpdatedAt = clock.NowLocal;
            reg.UpdatedBy = "SYSTEM_INBOUND_PROCESSOR";

            await uow.ExecuteInTransactionAsync(async innerCt =>
            {
                await regRepo.UpdateAsync(reg, innerCt);
            }, ct);

            await audit.LogAsync("ERP_INBOUND_VALIDATION_FAILED", nameof(VehicleRegistration), reg.Id, 
                new { reg.VehiclePlate, reg.CustomerCode, reg.ProductCode }, ct);

            return;
        }

        // 3. Process
        await uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = clock.NowLocal;

            // A. Upsert Vehicle
            var existingVehicle = await vehicleRepo.GetByPlateAndMoocAsync(reg.VehiclePlate, reg.MoocNumber, innerCt);
            if (existingVehicle == null)
            {
                existingVehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    VehiclePlate = reg.VehiclePlate,
                    MoocNumber = reg.MoocNumber,
                    DriverName = reg.ReceiverName,
                    TransportMethod = reg.TransportMethod?.ToString(),
                    CreatedAt = now,
                    CreatedBy = "SYSTEM_INBOUND_PROCESSOR"
                };
                await vehicleRepo.AddAsync(existingVehicle, innerCt);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(reg.ReceiverName)) existingVehicle.DriverName = reg.ReceiverName;
                if (reg.TransportMethod.HasValue) existingVehicle.TransportMethod = reg.TransportMethod.Value.ToString();
                existingVehicle.UpdatedAt = now;
                existingVehicle.UpdatedBy = "SYSTEM_INBOUND_PROCESSOR";
            }

            // B. Upsert Customer
            var existingCustomer = await customerRepo.GetByCodeAsync(reg.CustomerCode, innerCt);
            if (existingCustomer == null)
            {
                existingCustomer = new Customer
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = reg.CustomerCode,
                    CustomerName = reg.CustomerName ?? string.Empty,
                    CreatedAt = now,
                    CreatedBy = "SYSTEM_INBOUND_PROCESSOR"
                };
                await customerRepo.AddAsync(existingCustomer, innerCt);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(reg.CustomerName)) existingCustomer.CustomerName = reg.CustomerName;
                existingCustomer.UpdatedAt = now;
                existingCustomer.UpdatedBy = "SYSTEM_INBOUND_PROCESSOR";
            }

            // C. Upsert Product
            var existingProduct = await productRepo.GetByCodeAsync(reg.ProductCode, innerCt);
            if (existingProduct == null)
            {
                existingProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    ProductCode = reg.ProductCode,
                    ProductName = reg.ProductName ?? string.Empty,
                    CreatedAt = now,
                    CreatedBy = "SYSTEM_INBOUND_PROCESSOR"
                };
                await productRepo.AddAsync(existingProduct, innerCt);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(reg.ProductName)) existingProduct.ProductName = reg.ProductName;
                existingProduct.UpdatedAt = now;
                existingProduct.UpdatedBy = "SYSTEM_INBOUND_PROCESSOR";
            }

            // D. Set Root Data
            reg.IsInboundProcessed = true;
            reg.InboundProcessedAt = now;
            reg.LastInboundAttemptAt = now;
            reg.InboundErrorCode = null;
            reg.InboundErrorMessage = null;
            reg.UpdatedAt = now;
            reg.UpdatedBy = "SYSTEM_INBOUND_PROCESSOR";

            if (reg.IsCancelled)
            {
                reg.RegistrationStatus = RegistrationStatus.CANCELLED; 
            }
            else
            {
                reg.RegistrationStatus = RegistrationStatus.REGISTERED;
            }

            await regRepo.UpdateAsync(reg, innerCt);
        }, ct);

        _logger.LogInformation("Successfully processed ERP Registration {Id}", reg.Id);
    }

    private void LogPerf(string operation, double durationMs)
    {
        var entry = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            MachineName = Environment.MachineName,
            ThreadId = Environment.CurrentManagedThreadId,
            Operation = operation,
            DurationMs = Math.Round(durationMs, 2)
        };
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            var filePath = Path.Combine(logDir, "perf_metrics.jsonl");
            lock (this) { File.AppendAllText(filePath, json + Environment.NewLine); }
        }
        catch { }
    }
}


public static class ServiceScopeFactoryExtensions
{
    public static IServiceScope CreateFactoryScope(this IServiceScopeFactory factory)
    {
        return factory.CreateScope();
    }
}
