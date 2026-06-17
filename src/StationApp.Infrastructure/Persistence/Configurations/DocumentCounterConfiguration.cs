using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence.Configurations;

public class DocumentCounterConfiguration : IEntityTypeConfiguration<DocumentCounter>
{
    public void Configure(EntityTypeBuilder<DocumentCounter> builder)
    {
        builder.ToTable("document_counters");
        builder.HasKey(e => e.CounterKey);

        builder.Property(e => e.CounterKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastValue).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }
}
