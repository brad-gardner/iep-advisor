using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StudentInviteConfiguration : IEntityTypeConfiguration<StudentInvite>
{
    public void Configure(EntityTypeBuilder<StudentInvite> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InviteEmail).HasMaxLength(256);
        builder.Property(i => i.InviteToken).HasMaxLength(128);

        builder.HasIndex(i => i.InviteToken);
        builder.HasIndex(i => i.InviteEmail);

        // Nullable parent-side / school-side targets; Restrict so the target can't be silently removed.
        builder.HasOne(i => i.ChildProfile)
            .WithMany()
            .HasForeignKey(i => i.ChildProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.SchoolStudent)
            .WithMany()
            .HasForeignKey(i => i.SchoolStudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
