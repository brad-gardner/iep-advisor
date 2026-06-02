using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionGoalConfiguration : IEntityTypeConfiguration<IepVersionGoal>
{
    public void Configure(EntityTypeBuilder<IepVersionGoal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Domain).HasMaxLength(150);
        builder.Property(g => g.Baseline).HasMaxLength(2000);
        builder.Property(g => g.TargetCriteria).HasMaxLength(2000);
        builder.Property(g => g.MeasurementMethod).HasMaxLength(1000);
        builder.Property(g => g.Timeframe).HasMaxLength(200);

        builder.HasOne(g => g.IepVersion)
            .WithMany(v => v.Goals)
            .HasForeignKey(g => g.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.IepVersionId);
        builder.HasIndex(g => g.LineageId);
    }
}
