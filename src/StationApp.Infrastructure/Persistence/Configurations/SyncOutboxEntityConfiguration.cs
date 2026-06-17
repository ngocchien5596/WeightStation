using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class SyncOutboxEntityConfiguration : IEntityTypeConfiguration<SyncOutbox>
{
    public void Configure(EntityTypeBuilder<SyncOutbox> builder)
    {
        builder.ToTable("sync_outbox");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StationCode).HasMaxLength(50).IsRequired().HasDefaultValue("QN01");
        builder.Property(e => e.AggregateId).IsRequired();
        builder.Property(e => e.AggregateType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.IdempotencyKey).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastError).HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.StationCode, e.Status, e.NextRetryAt }).HasDatabaseName("IX_sync_outbox_station_status_next_retry");
        builder.HasIndex(e => e.AggregateId).HasDatabaseName("IX_sync_outbox_aggregate_id");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("IX_sync_outbox_idempotency_key");
    }
}
