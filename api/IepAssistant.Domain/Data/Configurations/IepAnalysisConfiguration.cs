using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepAnalysisConfiguration : IEntityTypeConfiguration<IepAnalysis>
{
    public void Configure(EntityTypeBuilder<IepAnalysis> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasMaxLength(20).IsRequired();
        builder.Property(a => a.OverallSummary).HasMaxLength(5000);
        builder.Property(a => a.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(a => a.IepDocument)
            .WithMany()
            .HasForeignKey(a => a.IepDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.IepDocumentId);
    }
}
