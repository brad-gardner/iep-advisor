using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AnalysisRunConfiguration : IEntityTypeConfiguration<AnalysisRun>
{
    public void Configure(EntityTypeBuilder<AnalysisRun> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.OverallSummary).HasMaxLength(5000);
        builder.Property(a => a.ErrorMessage).HasMaxLength(2000);
        builder.Property(a => a.FailureKind).HasMaxLength(32);
        builder.Property(a => a.BackfillSourceKey).HasMaxLength(64);

        // Filtered unique index: enforces one run per legacy analysis (idempotent backfill) while
        // allowing unlimited normal runs (BackfillSourceKey == null).
        builder.HasIndex(a => a.BackfillSourceKey)
            .IsUnique()
            .HasFilter("[BackfillSourceKey] IS NOT NULL");

        builder.HasOne(a => a.ChildProfile)
            .WithMany()
            .HasForeignKey(a => a.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ChildProfileId);
    }
}
