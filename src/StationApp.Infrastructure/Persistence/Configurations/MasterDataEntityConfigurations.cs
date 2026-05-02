using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class VehicleEntityConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.VehiclePlate).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MoocNumber).HasMaxLength(30).IsRequired().HasDefaultValue("");
        builder.Property(e => e.DriverName).HasMaxLength(100);
        builder.Property(e => e.TransportMethod).HasMaxLength(20);
        builder.Property(e => e.TtcpWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.VehicleRegistrationNo).HasMaxLength(50);
        builder.Property(e => e.MoocRegistrationNo).HasMaxLength(50);

        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => new { e.VehiclePlate, e.MoocNumber }).IsUnique().HasDatabaseName("UX_vehicles_plate_mooc");
        builder.HasIndex(e => e.VehiclePlate).HasDatabaseName("IX_vehicles_plate");
        builder.HasIndex(e => e.IsActive).HasDatabaseName("IX_vehicles_is_active");
    }
}

public class CustomerEntityConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CustomerCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CustomerName).HasMaxLength(255).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.CustomerCode).IsUnique().HasDatabaseName("UX_customers_code");
    }
}

public class ProductEntityConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProductCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ProductName).HasMaxLength(255).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.ProductCode).IsUnique().HasDatabaseName("UX_products_code");
    }
}

public class DeliveryTicketEntityConfiguration : IEntityTypeConfiguration<DeliveryTicket>
{
    public void Configure(EntityTypeBuilder<DeliveryTicket> builder)
    {
        builder.ToTable("delivery_tickets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WeighingSessionId);
        builder.Property(e => e.WeighingSessionLineId);

        builder.Property(e => e.DeliveryNo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ErpVehicleRegistrationId).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CustomerCode).HasMaxLength(50);
        builder.Property(e => e.ProductCode).HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.RecordRole).HasMaxLength(20).IsRequired().HasDefaultValue("NORMAL");
        builder.Property(e => e.AllocatedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.LastPrintedAt);
        builder.Property(e => e.LastPrintError).HasMaxLength(500);

        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.DeliveryNo).IsUnique().HasDatabaseName("UX_delivery_tickets_no");
        builder.HasIndex(e => e.ErpVehicleRegistrationId).HasDatabaseName("IX_delivery_tickets_erp_reg_id");
        builder.HasIndex(e => e.SplitGroupId).HasDatabaseName("IX_delivery_tickets_split_group_id");
        builder.HasIndex(e => e.IsPrinted).HasDatabaseName("IX_delivery_tickets_is_printed");
        builder.HasIndex(e => e.WeighingSessionId).HasDatabaseName("IX_delivery_tickets_weighing_session_id");
        builder.HasIndex(e => e.WeighingSessionLineId).HasDatabaseName("IX_delivery_tickets_weighing_session_line_id");
    }
}
