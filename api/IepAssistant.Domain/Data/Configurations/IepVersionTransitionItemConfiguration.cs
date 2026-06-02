using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionTransitionItemConfiguration : IEntityTypeConfiguration<IepVersionTransitionItem>
{
    public void Configure(EntityTypeBuilder<IepVersionTransitionItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.PostsecondaryGoalArea).HasMaxLength(200);

        builder.HasOne(t => t.IepVersion)
            .WithMany(v => v.TransitionItems)
            .HasForeignKey(t => t.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.IepVersionId);
        builder.HasIndex(t => t.LineageId);
    }
}
