using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ProgressReportAnalysisConfiguration : IEntityTypeConfiguration<ProgressReportAnalysis>
{
    public void Configure(EntityTypeBuilder<ProgressReportAnalysis> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(5000);
        builder.Property(a => a.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(a => a.ProgressReport)
            .WithMany()
            .HasForeignKey(a => a.ProgressReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ProgressReportId).IsUnique();
    }
}
