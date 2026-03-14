using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.GoalText).IsRequired();
        builder.Property(g => g.Domain).HasMaxLength(100);
        builder.Property(g => g.MeasurementMethod).HasMaxLength(500);
        builder.Property(g => g.Timeframe).HasMaxLength(200);

        builder.HasOne(g => g.IepSection)
            .WithMany(s => s.Goals)
            .HasForeignKey(g => g.IepSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.IepSectionId);
    }
}
