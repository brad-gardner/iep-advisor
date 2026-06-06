using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.StateCode).HasMaxLength(2);

        // One StudentProfile per user — enforces the "single workspace per student account".
        builder.HasIndex(p => p.UserId).IsUnique();

        // One student account per child / per school record — a DB backstop for the one-pair
        // invariant that also makes a concurrent double-link race fail with a unique violation
        // rather than silently fusing records.
        builder.HasIndex(p => p.ChildProfileId).IsUnique().HasFilter("[ChildProfileId] IS NOT NULL");
        builder.HasIndex(p => p.SchoolStudentId).IsUnique().HasFilter("[SchoolStudentId] IS NOT NULL");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nullable parent-side / school-side links; Restrict so a profile never silently loses its pairing.
        builder.HasOne<ChildProfile>()
            .WithMany()
            .HasForeignKey(p => p.ChildProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SchoolStudent>()
            .WithMany()
            .HasForeignKey(p => p.SchoolStudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
