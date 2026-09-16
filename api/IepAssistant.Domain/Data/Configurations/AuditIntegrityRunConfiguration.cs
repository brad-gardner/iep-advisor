using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AuditIntegrityRunConfiguration : IEntityTypeConfiguration<AuditIntegrityRun>
{
    public void Configure(EntityTypeBuilder<AuditIntegrityRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // GET /api/admin/audit/integrity: last 10 runs, newest first.
        builder.HasIndex(r => r.StartedAt);
    }
}
