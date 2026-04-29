using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence.Configurations;

namespace StationApp.Infrastructure.Persistence;

public class StationDbContext : DbContext
{
    public StationDbContext(DbContextOptions<StationDbContext> options) : base(options) { }

    public DbSet<WeighTicket> WeighTickets => Set<WeighTicket>();
    public DbSet<VehicleRegistration> VehicleRegistrations => Set<VehicleRegistration>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DeliveryTicket> DeliveryTickets => Set<DeliveryTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new VehicleRegistrationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WeighTicketEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SyncOutboxEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AppConfigEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfigEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryTicketEntityConfiguration());
    }
}
