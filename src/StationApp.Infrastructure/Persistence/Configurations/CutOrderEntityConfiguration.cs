using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class CutOrderEntityConfiguration : IEntityTypeConfiguration<CutOrder>
{
    public void Configure(EntityTypeBuilder<CutOrder> builder)
    {
        builder.ToTable("cut_orders", tableBuilder =>
        {
            tableBuilder.HasTrigger("TR_cut_orders_enforce_active_erp_cut_order_id");
            tableBuilder.UseSqlOutputClause(false);
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ErpCutOrderId).HasColumnName("ErpCutOrderId").HasMaxLength(50);
        builder.Property(e => e.ErpRegistrationCode).HasColumnName("ErpRegistrationCode").HasMaxLength(100);
        builder.Property(e => e.CutOrderSource).HasColumnName("CutOrderSource").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.CutOrderStatus).HasColumnName("CutOrderStatus").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.TransportMethod).HasConversion<string>().HasMaxLength(20);

        builder.Property(e => e.VehiclePlate).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MoocNumber).HasMaxLength(30);
        builder.Property(e => e.ReceiverName).HasMaxLength(100);
        builder.Property(e => e.ReceiverIdNo).HasMaxLength(50);

        builder.Property(e => e.CustomerCode).HasMaxLength(50);
        builder.Property(e => e.CustomerName).HasMaxLength(255);

        builder.Property(e => e.ProductCode).HasMaxLength(50);
        builder.Property(e => e.ProductName).HasMaxLength(255);
        builder.Property(e => e.ProductType).HasMaxLength(30);
        builder.Property(e => e.OrderCode).HasMaxLength(100);
        builder.Property(e => e.LotNo).HasMaxLength(100);
        builder.Property(e => e.RepresentativeName).HasMaxLength(150);
        builder.Property(e => e.Market).HasMaxLength(255);
        builder.Property(e => e.ConsumptionPlace).HasMaxLength(255);
        builder.Property(e => e.LoadingPlace).HasMaxLength(255);
        builder.Property(e => e.SealNo).HasMaxLength(100);

        builder.Property(e => e.PlannedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedAt);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.HasOverweightCase).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ProcessingStage)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(StationApp.Domain.Enums.ProcessingStage.IN_YARD)
            .HasSentinel(StationApp.Domain.Enums.ProcessingStage.IN_YARD);
        builder.Property(e => e.WeighingSessionId);
        builder.Property(e => e.CarryForwardWeight1).HasColumnType("decimal(18,3)");
        builder.Property(e => e.CarryForwardWeight1Time);
        builder.Property(e => e.IsExportScale).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ExportFinalizedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.ExportFinalizedBy).HasMaxLength(100);
        builder.Property(e => e.ExportStartedBy).HasMaxLength(100);
        builder.Property(e => e.ErpExportCompleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsTemporaryExport).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.MappedRealCutOrderId);
        builder.Property(e => e.MappedTemporaryCutOrderId);
        builder.Property(e => e.TemporaryExportCreatedReason).HasMaxLength(50);
        builder.Property(e => e.TemporaryExportDisplayCode).HasMaxLength(100);
        builder.Property(e => e.TemporaryExportSourceErpCutOrderId).HasMaxLength(100);
        builder.Property(e => e.MappedAt);
        builder.Property(e => e.MappedBy).HasMaxLength(100);

        builder.Property(e => e.IsInboundProcessed).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.InboundProcessedAt);
        builder.Property(e => e.InboundErrorCode).HasMaxLength(50);
        builder.Property(e => e.InboundErrorMessage).HasMaxLength(500);


        builder.Property(e => e.LastSyncAttemptAt);
        builder.Property(e => e.LastSyncError).HasMaxLength(500);

        builder.Property(e => e.SyncStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.IdempotencyKey).IsRequired();
        builder.Property(e => e.AppVersion).HasMaxLength(50);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        // Indexes
        builder.HasIndex(e => e.CutOrderStatus).HasDatabaseName("IX_cut_orders_status");
        builder.HasIndex(e => e.SyncStatus).HasDatabaseName("IX_cut_orders_sync_status");
        builder.HasIndex(e => e.VehiclePlate).HasDatabaseName("IX_cut_orders_vehicle_plate");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_cut_orders_created_at");
        builder.HasIndex(e => new { e.ProcessingStage, e.IsCancelled, e.IsDeleted }).HasDatabaseName("IX_cut_orders_processing_stage");
        builder.HasIndex(e => e.WeighingSessionId).HasDatabaseName("IX_cut_orders_weighing_session_id");
        builder.HasIndex(e => new { e.IsExportScale, e.CutOrderStatus, e.ProcessingStage, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_is_export_scale_status");
        builder.HasIndex(e => new { e.IsTemporaryExport, e.IsExportScale, e.ProcessingStage, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_temp_export");
        builder.HasIndex(e => new { e.MappedRealCutOrderId, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_mapped_real");
        builder.HasIndex(e => new { e.TemporaryExportSourceErpCutOrderId, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_temp_source_erp");
        
        builder.HasIndex(e => new { e.ErpCutOrderId, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_erp_cut_order_id_deleted");
        builder.HasIndex(e => new { e.ErpRegistrationCode, e.IsDeleted })
               .HasDatabaseName("IX_cut_orders_erp_registration_code_deleted");
    }
}

