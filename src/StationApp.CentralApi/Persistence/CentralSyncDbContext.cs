using Microsoft.EntityFrameworkCore;
using StationApp.Infrastructure.Persistence.Configurations;
using StationApp.Domain.Entities;

namespace StationApp.CentralApi.Persistence;

public sealed class CentralSyncDbContext : DbContext
{
    public CentralSyncDbContext(DbContextOptions<CentralSyncDbContext> options) : base(options)
    {
    }

    public DbSet<CutOrder> CutOrders => Set<CutOrder>();
    public DbSet<WeighTicket> WeighTickets => Set<WeighTicket>();
    public DbSet<DeliveryTicket> DeliveryTickets => Set<DeliveryTicket>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WeighingSession> WeighingSessions => Set<WeighingSession>();
    public DbSet<WeighingSessionLine> WeighingSessionLines => Set<WeighingSessionLine>();
    public DbSet<WeighingSessionImage> WeighingSessionImages => Set<WeighingSessionImage>();
    public DbSet<SyncIngestionLog> SyncIngestionLogs => Set<SyncIngestionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CutOrderEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighTicketEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryTicketEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionLineEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighingSessionImageEntityConfiguration());

        modelBuilder.Entity<SyncIngestionLog>(builder =>
        {
            builder.ToTable("sync_ingestion_logs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.AggregateType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
            builder.Property(x => x.ErrorMessage).HasMaxLength(4000);
            builder.HasIndex(x => new { x.AggregateType, x.SourceRecordId });
            builder.HasIndex(x => x.ReceivedAt);
        });
    }
}
