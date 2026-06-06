using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ChildLinkConfiguration : IEntityTypeConfiguration<ChildLink>
{
    public void Configure(EntityTypeBuilder<ChildLink> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.InviteEmail).HasMaxLength(256);
        builder.Property(l => l.InviteToken).HasMaxLength(128);

        builder.HasOne(l => l.SchoolStudent)
            .WithMany()
            .HasForeignKey(l => l.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nullable FK (link may exist as an invite before a ChildProfile is created/accepted).
        builder.HasOne(l => l.ChildProfile)
            .WithMany()
            .HasForeignKey(l => l.ChildProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.SchoolStudentId);
        builder.HasIndex(l => l.ChildProfileId);
        builder.HasIndex(l => l.InviteToken);

        builder.HasIndex(l => new { l.SchoolStudentId, l.ChildProfileId })
            .IsUnique()
            .HasFilter("[ChildProfileId] IS NOT NULL");
    }
}
