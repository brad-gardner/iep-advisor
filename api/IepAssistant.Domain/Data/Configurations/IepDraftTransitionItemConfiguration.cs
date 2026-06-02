using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftTransitionItemConfiguration : IEntityTypeConfiguration<IepDraftTransitionItem>
{
    public void Configure(EntityTypeBuilder<IepDraftTransitionItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.PostsecondaryGoalArea).HasMaxLength(200);

        builder.HasOne(t => t.IepDraft)
            .WithMany(d => d.TransitionItems)
            .HasForeignKey(t => t.IepDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.IepDraftId);
        builder.HasIndex(t => t.LineageId);
    }
}
