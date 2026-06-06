using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AnalysisRunSourceConfiguration : IEntityTypeConfiguration<AnalysisRunSource>
{
    public void Configure(EntityTypeBuilder<AnalysisRunSource> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SourceType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.SourceLabel).HasMaxLength(300);

        builder.HasOne(s => s.AnalysisRun)
            .WithMany(r => r.Sources)
            .HasForeignKey(s => s.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.AnalysisRunId);
    }
}
