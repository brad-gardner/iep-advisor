using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AdvocateThreadConfiguration : IEntityTypeConfiguration<AdvocateThread>
{
    public const int TitleMaxLength = 120;

    public void Configure(EntityTypeBuilder<AdvocateThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(TitleMaxLength).IsRequired();

        builder.HasOne(t => t.ChildProfile)
            .WithMany()
            .HasForeignKey(t => t.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict like every other parent-authored row (ParentDraftNote, UsageRecord): a User row is only
        // removed by AccountPurgeService, which deletes this parent's threads first.
        builder.HasOne(t => t.ParentUser)
            .WithMany()
            .HasForeignKey(t => t.ParentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Thread)
            .HasForeignKey(m => m.AdvocateThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // The thread list: this parent's threads for this child, most recently active first.
        builder.HasIndex(t => new { t.ParentUserId, t.ChildProfileId, t.LastMessageAt }).IsDescending(false, false, true);
    }
}
