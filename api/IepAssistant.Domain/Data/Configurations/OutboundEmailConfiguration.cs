using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class OutboundEmailConfiguration : IEntityTypeConfiguration<OutboundEmail>
{
    public void Configure(EntityTypeBuilder<OutboundEmail> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ToEmail).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.HtmlBody).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(1000);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);

        // OutboundEmailWorker's claim query: due, still-queued rows in fairness (oldest-first) order.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });

        // GET /api/admin/email?status=...: filtered, newest-first.
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}
