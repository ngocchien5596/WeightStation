using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class StationOperationSettingEntityConfiguration : IEntityTypeConfiguration<StationOperationSetting>
{
    public void Configure(EntityTypeBuilder<StationOperationSetting> builder)
    {
        builder.ToTable("station_operation_settings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.StationCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SettingValue).HasMaxLength(1000).IsRequired().HasDefaultValue("");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => new { e.StationCode, e.SettingKey })
            .IsUnique()
            .HasDatabaseName("UX_station_operation_settings_station_key");
    }
}
