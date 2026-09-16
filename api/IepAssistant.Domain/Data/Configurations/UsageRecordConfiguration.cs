using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.OperationType)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nullable since plan 6 (a staff-triggered, district-billed operation may have no child profile).
        builder.HasOne(ur => ur.ChildProfile)
            .WithMany()
            .HasForeignKey(ur => ur.ChildProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Plan 6: district-sponsored usage (draft explanations/questions, meeting summaries) attributes
        // billing to the school rather than (or in addition to) the parent's own subscription.
        builder.HasOne(ur => ur.District)
            .WithMany()
            .HasForeignKey(ur => ur.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.ChildProfileId);
        builder.HasIndex(ur => ur.DistrictId);
    }
}
