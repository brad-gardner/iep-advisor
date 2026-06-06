using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AnalysisRunSectionConfiguration : IEntityTypeConfiguration<AnalysisRunSection>
{
    public void Configure(EntityTypeBuilder<AnalysisRunSection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SectionKind).HasMaxLength(50).IsRequired();

        builder.HasOne(s => s.AnalysisRun)
            .WithMany(r => r.Sections)
            .HasForeignKey(s => s.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.AnalysisRunId);

        // AnalysisRunSourceId is a loose int? (no hard FK) to avoid cascade-path conflicts.
        builder.HasIndex(s => s.AnalysisRunSourceId);
    }
}
