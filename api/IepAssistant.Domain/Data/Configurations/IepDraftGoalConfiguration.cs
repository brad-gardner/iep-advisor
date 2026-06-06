using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftGoalConfiguration : IEntityTypeConfiguration<IepDraftGoal>
{
    public void Configure(EntityTypeBuilder<IepDraftGoal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Domain).HasMaxLength(150);
        builder.Property(g => g.Baseline).HasMaxLength(2000);
        builder.Property(g => g.TargetCriteria).HasMaxLength(2000);
        builder.Property(g => g.MeasurementMethod).HasMaxLength(1000);
        builder.Property(g => g.Timeframe).HasMaxLength(200);

        builder.HasOne(g => g.IepDraft)
            .WithMany(d => d.Goals)
            .HasForeignKey(g => g.IepDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.IepDraftId);
        builder.HasIndex(g => g.LineageId);
    }
}
