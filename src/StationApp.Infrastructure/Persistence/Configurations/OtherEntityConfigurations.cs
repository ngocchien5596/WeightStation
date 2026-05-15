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
            new AppConfig { ConfigKey = "tolerance_kg", ConfigValue = "500", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "sync_interval_seconds", ConfigValue = "30", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "retry_base_seconds", ConfigValue = "30", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "device_com_port", ConfigValue = "COM1", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "device_baudrate", ConfigValue = "9600", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = "device_parser_type", ConfigValue = "DEFAULT", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new AppConfig { ConfigKey = AppConfigKeys.OverweightSplitStepWeight, ConfigValue = "0.0025", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "SYSTEM", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
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
