using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ParentAdvocacyGoalConfiguration : IEntityTypeConfiguration<ParentAdvocacyGoal>
{
    public void Configure(EntityTypeBuilder<ParentAdvocacyGoal> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.GoalText).HasMaxLength(500).IsRequired();
        builder.Property(g => g.Category).HasMaxLength(50);

        builder.HasOne(g => g.ChildProfile)
            .WithMany()
            .HasForeignKey(g => g.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.ChildProfileId);
    }
}
