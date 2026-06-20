using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class WeighTicketEntityConfiguration : IEntityTypeConfiguration<WeighTicket>
{
    public void Configure(EntityTypeBuilder<WeighTicket> builder)
    {
        builder.ToTable("weigh_tickets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StationCode).HasMaxLength(50).IsRequired().HasDefaultValue("QN01");
        builder.Property(e => e.CutOrderId).IsRequired();
        builder.Property(e => e.WeighingSessionId);

        builder.Property(e => e.TicketNo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ErpCutOrderId).HasMaxLength(50);
        builder.Property(e => e.VehiclePlate).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MoocNumber).HasMaxLength(30);
        builder.Property(e => e.DriverName).HasMaxLength(100);
        builder.Property(e => e.CustomerCode).HasMaxLength(50);
        builder.Property(e => e.CustomerName).HasMaxLength(255);
        builder.Property(e => e.ProductCode).HasMaxLength(50);
        builder.Property(e => e.ProductName).HasMaxLength(255);
        builder.Property(e => e.PlannedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.TransportMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.IdempotencyKey).IsRequired();
        builder.Property(e => e.SyncStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(e => e.Weight1).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Weight1User).HasMaxLength(100);
        builder.Property(e => e.Weight1Mode).HasConversion<string>().HasMaxLength(20);

        builder.Property(e => e.Weight2).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Weight2User).HasMaxLength(100);
        builder.Property(e => e.Weight2Mode).HasConversion<string>().HasMaxLength(20);

        builder.Property(e => e.NetWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.AppVersion).HasMaxLength(50);
        builder.Property(e => e.WeighingMode).HasMaxLength(40).IsRequired().HasDefaultValue("TWO_WEIGH");
        builder.Property(e => e.InternalVehicleNo).HasMaxLength(30);
        builder.Property(e => e.StandardTareWeightSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(e => e.StandardTareSourceSnapshot).HasMaxLength(50);
        builder.Property(e => e.NetWeightCalculationMode).HasMaxLength(50).HasDefaultValue("WEIGHT2_DIFF");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        // Phase 2 Delta Snapshots
        builder.Property(e => e.Ttcp10WeightSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(e => e.VehicleRegistrationNoSnapshot).HasMaxLength(50);
        builder.Property(e => e.MoocRegistrationNoSnapshot).HasMaxLength(50);

        // Phase 2 Delta Logic
        builder.Property(e => e.RecordRole).HasMaxLength(20).IsRequired().HasDefaultValue("MASTER_SESSION");
        builder.Property(e => e.IsPrimaryDisplay).IsRequired();
        builder.Property(e => e.IsOverWeight).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsPrinted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.LastPrintedAt);
        builder.Property(e => e.LastPrintError).HasMaxLength(500);

        // Phase 2 Indexes
        builder.HasIndex(e => e.SplitGroupId).HasDatabaseName("IX_weigh_tickets_split_group_id");
        builder.HasIndex(e => e.RecordRole).HasDatabaseName("IX_weigh_tickets_record_role");

        // Indexes per Phase 0 spec section 9.1
        builder.HasIndex(e => new { e.StationCode, e.TicketNo }).IsUnique().HasDatabaseName("UX_weigh_tickets_station_ticket_no");
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("UX_weigh_tickets_idempotency_key");
        builder.HasIndex(e => e.ErpCutOrderId).HasDatabaseName("IX_weigh_tickets_erp_vehicle_registration_id");
        builder.HasIndex(e => e.VehiclePlate).HasDatabaseName("IX_weigh_tickets_vehicle_plate");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_weigh_tickets_status");
        builder.HasIndex(e => e.SyncStatus).HasDatabaseName("IX_weigh_tickets_sync_status");
        builder.HasIndex(e => new { e.StationCode, e.CreatedAt }).HasDatabaseName("IX_weigh_tickets_station_created_at");
        builder.HasIndex(e => e.WeighingSessionId).HasDatabaseName("IX_weigh_tickets_weighing_session_id");
    }
}


