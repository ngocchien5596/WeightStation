using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Actor).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EntityId).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => e.CreatedAt);
    }
}

public class AppConfigEntityConfiguration : IEntityTypeConfiguration<AppConfig>
{
    public void Configure(EntityTypeBuilder<AppConfig> builder)
    {
        builder.ToTable("app_config");
        builder.HasKey(e => e.ConfigKey);
        builder.Property(e => e.ConfigKey).HasMaxLength(100);
        builder.Property(e => e.ConfigValue).HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasData(
            new AppConfig { ConfigKey = "station_code", ConfigValue = "QN01", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "ticket_prefix", ConfigValue = "QN", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.ToleranceKgPerBag, ConfigValue = AppConfigDefaults.DefaultToleranceKgPerBag.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "sync_interval_seconds", ConfigValue = "30", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "retry_base_seconds", ConfigValue = "30", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceComPort, ConfigValue = AppConfigDefaults.DefaultDeviceComPort, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceBaudrate, ConfigValue = AppConfigDefaults.DefaultDeviceBaudrate, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceParity, ConfigValue = AppConfigDefaults.DefaultDeviceParity, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceDataBits, ConfigValue = AppConfigDefaults.DefaultDeviceDataBits, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceStopBits, ConfigValue = AppConfigDefaults.DefaultDeviceStopBits, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceParserType, ConfigValue = AppConfigDefaults.DefaultDeviceParserType, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceFrameEndChar, ConfigValue = AppConfigDefaults.DefaultDeviceFrameEndChar, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.DeviceStableCycles, ConfigValue = AppConfigDefaults.DefaultDeviceStableCycles, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.WeightSubstringStart, ConfigValue = AppConfigDefaults.DefaultWeightSubstringStart, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.WeightSubstringLength, ConfigValue = AppConfigDefaults.DefaultWeightSubstringLength, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.OverweightSplitStepWeight, ConfigValue = "0.0025", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera1Enabled, ConfigValue = AppConfigDefaults.DefaultCamera1Enabled, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera1Name, ConfigValue = AppConfigDefaults.DefaultCamera1Name, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera1RtspUrl, ConfigValue = AppConfigDefaults.DefaultCamera1RtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera1PreviewRtspUrl, ConfigValue = AppConfigDefaults.DefaultCamera1PreviewRtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera2Enabled, ConfigValue = AppConfigDefaults.DefaultCamera2Enabled, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera2Name, ConfigValue = AppConfigDefaults.DefaultCamera2Name, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera2RtspUrl, ConfigValue = AppConfigDefaults.DefaultCamera2RtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.Camera2PreviewRtspUrl, ConfigValue = AppConfigDefaults.DefaultCamera2PreviewRtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_1Enabled, ConfigValue = AppConfigDefaults.DefaultCameraC6_1Enabled, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_1Name, ConfigValue = AppConfigDefaults.DefaultCameraC6_1Name, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_1RtspUrl, ConfigValue = AppConfigDefaults.DefaultCameraC6_1RtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_1PreviewRtspUrl, ConfigValue = AppConfigDefaults.DefaultCameraC6_1PreviewRtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_2Enabled, ConfigValue = AppConfigDefaults.DefaultCameraC6_2Enabled, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_2Name, ConfigValue = AppConfigDefaults.DefaultCameraC6_2Name, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_2RtspUrl, ConfigValue = AppConfigDefaults.DefaultCameraC6_2RtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraC6_2PreviewRtspUrl, ConfigValue = AppConfigDefaults.DefaultCameraC6_2PreviewRtspUrl, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraPreviewDefault, ConfigValue = AppConfigDefaults.DefaultCameraPreview, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraCaptureTimeoutMs, ConfigValue = AppConfigDefaults.DefaultCameraCaptureTimeoutMs, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraCaptureJpegQuality, ConfigValue = AppConfigDefaults.DefaultCameraCaptureJpegQuality, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.CameraCaptureWarmupFrames, ConfigValue = AppConfigDefaults.DefaultCameraCaptureWarmupFrames, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}

public class DeviceConfigEntityConfiguration : IEntityTypeConfiguration<DeviceConfig>
{
    public void Configure(EntityTypeBuilder<DeviceConfig> builder)
    {
        builder.ToTable("device_configs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DeviceName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ComPort).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Baudrate).IsRequired();
        builder.Property(e => e.Parity).HasMaxLength(20);
        builder.Property(e => e.FrameEndChar).HasMaxLength(10);
        builder.Property(e => e.ParserType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.StabilityThreshold).HasColumnType("decimal(18,3)");
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
    }
}

public class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.RoleCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(255);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.Username).IsUnique();

        builder.HasData(new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            DisplayName = "Administrator",
            RoleCode = "ADMIN",
            IsActive = true,
            PasswordHash = "$2a$11$163R2ooQUQJV1vz3PUT.suSHkSkXzm9uqReEbooIM8MoZayruJsAm", // admin123
            LastLoginAt = null,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "SYSTEM",
            UpdatedAt = null,
            UpdatedBy = null
        });
    }
}

public class PrintTemplateProfileEntityConfiguration : IEntityTypeConfiguration<PrintTemplateProfile>
{
    public void Configure(EntityTypeBuilder<PrintTemplateProfile> builder)
    {
        builder.ToTable("print_template_profiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TemplateKind).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ProfileKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired();
        builder.Property(e => e.OffsetXmm).HasColumnType("decimal(18,3)");
        builder.Property(e => e.OffsetYmm).HasColumnType("decimal(18,3)");
        builder.Property(e => e.TemplateVersion).IsRequired();
        builder.Property(e => e.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => new { e.TemplateKind, e.ProfileKey }).IsUnique();
        builder.HasIndex(e => new { e.TemplateKind, e.IsDefault });
    }
}
