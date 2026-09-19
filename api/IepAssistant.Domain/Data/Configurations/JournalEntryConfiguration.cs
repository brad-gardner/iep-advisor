using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public const int ContentMaxLength = 4000;

    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Tag).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(j => j.ContentMarkdown).HasMaxLength(ContentMaxLength).IsRequired();

        builder.HasOne(j => j.ChildProfile)
            .WithMany()
            .HasForeignKey(j => j.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional links are NoAction (not SetNull/Cascade): the ChildProfile FK above already cascades and
        // IepDocument/EtrDocument cascade from ChildProfile too, so any second cascading path here would be
        // rejected by SQL Server ("multiple cascade paths"). Documents are soft-deleted and meetings are
        // cancelled rather than deleted, and AccountPurgeService removes journal entries before documents.
        builder.HasOne(j => j.LinkedIepDocument)
            .WithMany()
            .HasForeignKey(j => j.LinkedIepDocumentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(j => j.LinkedEtrDocument)
            .WithMany()
            .HasForeignKey(j => j.LinkedEtrDocumentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(j => j.LinkedMeeting)
            .WithMany()
            .HasForeignKey(j => j.LinkedMeetingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(j => new { j.ChildProfileId, j.OccurredOn }).IsDescending(false, true);
        builder.HasIndex(j => j.LinkedIepDocumentId);
        builder.HasIndex(j => j.LinkedEtrDocumentId);
        builder.HasIndex(j => j.LinkedMeetingId);
    }
}
