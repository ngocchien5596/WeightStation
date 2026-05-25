using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class WeighingSessionEntityConfiguration : IEntityTypeConfiguration<WeighingSession>
{
    public void Configure(EntityTypeBuilder<WeighingSession> builder)
    {
        builder.ToTable("weighing_sessions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SessionNo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.VehiclePlate).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MoocNumber).HasMaxLength(30);
        builder.Property(e => e.DriverName).HasMaxLength(150);
        builder.Property(e => e.SessionStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(e => e.Weight1).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Weight2).HasColumnType("decimal(18,3)");
        builder.Property(e => e.NetWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Ttcp10WeightSnapshot).HasColumnType("decimal(18,3)");
        builder.Property(e => e.IsOverweight).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.OverweightAmount).HasColumnType("decimal(18,3)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.OverweightResolutionStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.OverweightResolvedBy).HasMaxLength(100);

        builder.Property(e => e.IsCancelled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.HasPrintedMasterWeighTicket).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.UseActualWeightForBaggedCutOrders).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.SessionNo).IsUnique().HasDatabaseName("UX_weighing_sessions_session_no");
        builder.HasIndex(e => e.VehiclePlate).HasDatabaseName("IX_weighing_sessions_vehicle_plate");
        builder.HasIndex(e => new { e.SessionStatus, e.IsDeleted }).HasDatabaseName("IX_weighing_sessions_status");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_weighing_sessions_created_at");
    }
}

public class WeighingSessionLineEntityConfiguration : IEntityTypeConfiguration<WeighingSessionLine>
{
    public void Configure(EntityTypeBuilder<WeighingSessionLine> builder)
    {
        builder.ToTable("weighing_session_lines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CustomerCode).HasMaxLength(50);
        builder.Property(e => e.CustomerName).HasMaxLength(255);
        builder.Property(e => e.DistributorCode).HasMaxLength(50);
        builder.Property(e => e.DistributorName).HasMaxLength(255);
        builder.Property(e => e.ProductCode).HasMaxLength(50);
        builder.Property(e => e.ProductName).HasMaxLength(255);
        builder.Property(e => e.PlannedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.ActualAllocatedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.LineStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.HasPrintedDeliveryTicket).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.WeighingSessionId).HasDatabaseName("IX_weighing_session_lines_session_id");
        builder.HasIndex(e => e.CutOrderId).HasDatabaseName("IX_weighing_session_lines_registration_id");

        builder.HasIndex(e => new { e.WeighingSessionId, e.CutOrderId })
            .IsUnique()
            .HasDatabaseName("UX_weighing_session_lines_session_registration");
    }
}

public class WeighingSessionImageEntityConfiguration : IEntityTypeConfiguration<WeighingSessionImage>
{
    public void Configure(EntityTypeBuilder<WeighingSessionImage> builder)
    {
        builder.ToTable("weighing_session_images");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CaptureStage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.CameraCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CameraName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RtspUrlSnapshot).HasMaxLength(1000);
        builder.Property(e => e.ImageFormat).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ImageBytes).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(e => e.CapturedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.WeighingSessionId).HasDatabaseName("IX_weighing_session_images_session_id");
        builder.HasIndex(e => new { e.WeighingSessionId, e.CaptureStage, e.CameraCode }).HasDatabaseName("IX_weighing_session_images_lookup");
        builder.HasIndex(e => e.CapturedAt).HasDatabaseName("IX_weighing_session_images_captured_at");
    }
}

