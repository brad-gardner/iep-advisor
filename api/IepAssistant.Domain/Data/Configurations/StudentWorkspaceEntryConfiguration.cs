using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StudentWorkspaceEntryConfiguration : IEntityTypeConfiguration<StudentWorkspaceEntry>
{
    public void Configure(EntityTypeBuilder<StudentWorkspaceEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryKind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.IsShareable)
            .HasDefaultValue(false);

        builder.HasOne(e => e.StudentWorkspace)
            .WithMany(w => w.Entries)
            .HasForeignKey(e => e.StudentWorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.StudentWorkspaceId);
    }
}
