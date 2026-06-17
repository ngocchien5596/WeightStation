using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence.Configurations;
using StationApp.Infrastructure.Services;

namespace StationApp.Infrastructure.Persistence;

public class StationDbContext : DbContext
{
    public StationDbContext(DbContextOptions<StationDbContext> options) : base(options) { }

    public DbSet<WeighTicket> WeighTickets => Set<WeighTicket>();
    public DbSet<CutOrder> CutOrders => Set<CutOrder>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<UserStationAssignment> UserStationAssignments => Set<UserStationAssignment>();
    public DbSet<StationFeatureFlag> StationFeatureFlags => Set<StationFeatureFlag>();
    public DbSet<StationOperationSetting> StationOperationSettings => Set<StationOperationSetting>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DeliveryTicket> DeliveryTickets => Set<DeliveryTicket>();
    public DbSet<WeighingSession> WeighingSessions => Set<WeighingSession>();
    public DbSet<WeighingSessionLine> WeighingSessionLines => Set<WeighingSessionLine>();
    public DbSet<WeighingSessionImage> WeighingSessionImages => Set<WeighingSessionImage>();
    public DbSet<PrintTemplateProfile> PrintTemplateProfiles => Set<PrintTemplateProfile>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyStationCodeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ApplyStationCodeAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ApplyStationCodeAsync(CancellationToken ct)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var stationCode = await ResolveCurrentStationCodeAsync(ct);
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return;
        }

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case CutOrder cutOrder when string.IsNullOrWhiteSpace(cutOrder.StationCode):
                    cutOrder.StationCode = stationCode;
                    break;
                case WeighingSession session when string.IsNullOrWhiteSpace(session.StationCode):
                    session.StationCode = stationCode;
                    break;
                case WeighingSessionLine line when string.IsNullOrWhiteSpace(line.StationCode):
                    line.StationCode = stationCode;
                    break;
                case WeighingSessionImage image when string.IsNullOrWhiteSpace(image.StationCode):
                    image.StationCode = stationCode;
                    break;
                case WeighTicket ticket when string.IsNullOrWhiteSpace(ticket.StationCode):
                    ticket.StationCode = stationCode;
                    break;
                case DeliveryTicket ticket when string.IsNullOrWhiteSpace(ticket.StationCode):
                    ticket.StationCode = stationCode;
                    break;
                case SyncOutbox outbox when string.IsNullOrWhiteSpace(outbox.StationCode):
                    outbox.StationCode = stationCode;
                    break;
                case Vehicle vehicle when string.IsNullOrWhiteSpace(vehicle.StationCode):
                    vehicle.StationCode = stationCode;
                    break;
                case Customer customer when string.IsNullOrWhiteSpace(customer.StationCode):
                    customer.StationCode = stationCode;
                    break;
                case Product product when string.IsNullOrWhiteSpace(product.StationCode):
                    product.StationCode = stationCode;
                    break;
            }
        }
    }

    private async Task<string> ResolveCurrentStationCodeAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(StationRuntimeScope.StationCode))
        {
            return StationRuntimeScope.StationCode!;
        }

        var localValue = ChangeTracker.Entries<AppConfig>()
            .Where(e => e.Entity.ConfigKey == AppConfigKeys.DefaultStationCode || e.Entity.ConfigKey == AppConfigKeys.StationCode)
            .Select(e => e.Entity.ConfigValue)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(localValue))
        {
            return localValue.Trim();
        }

        var dbValue = await AppConfigs
            .AsNoTracking()
            .Where(c => c.ConfigKey == AppConfigKeys.DefaultStationCode)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(dbValue))
        {
            return dbValue.Trim();
        }

        dbValue = await AppConfigs
            .AsNoTracking()
            .Where(c => c.ConfigKey == AppConfigKeys.StationCode)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(dbValue) ? "QN01" : dbValue.Trim();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CutOrderEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighTicketEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SyncOutboxEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AppConfigEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserStationAssignmentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StationFeatureFlagEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StationOperationSettingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryTicketEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionLineEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionImageEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PrintTemplateProfileEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentCounterConfiguration());
    }
}
