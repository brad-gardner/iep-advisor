using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class PendingAuditEventConfiguration : IEntityTypeConfiguration<PendingAuditEvent>
{
    public void Configure(EntityTypeBuilder<PendingAuditEvent> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ActionValue)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ResourceType)
            .HasMaxLength(50)
            .IsRequired();

        // Startup replay reads the whole table in Id (== enqueue order within a batch) order.
        builder.HasIndex(p => p.Id);
    }
}
