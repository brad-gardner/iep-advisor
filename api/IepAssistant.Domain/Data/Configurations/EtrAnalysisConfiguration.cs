using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class EtrAnalysisConfiguration : IEntityTypeConfiguration<EtrAnalysis>
{
    public void Configure(EntityTypeBuilder<EtrAnalysis> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasMaxLength(50).IsRequired();
        builder.Property(a => a.OverallSummary).HasMaxLength(5000);
        builder.Property(a => a.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(a => a.EtrDocument)
            .WithMany()
            .HasForeignKey(a => a.EtrDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.EtrDocumentId).IsUnique();
    }
}
