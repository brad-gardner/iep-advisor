using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StudentWorkspaceConfiguration : IEntityTypeConfiguration<StudentWorkspace>
{
    public void Configure(EntityTypeBuilder<StudentWorkspace> builder)
    {
        builder.HasKey(w => w.Id);

        // One workspace per student account.
        builder.HasIndex(w => w.UserId).IsUnique();

        // Restrict so a workspace never silently loses its owner; the student account is the legal subject.
        builder.HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Entries)
            .WithOne(e => e.StudentWorkspace)
            .HasForeignKey(e => e.StudentWorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
