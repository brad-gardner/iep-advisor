using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ParentDraftNoteConfiguration : IEntityTypeConfiguration<ParentDraftNote>
{
    public void Configure(EntityTypeBuilder<ParentDraftNote> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Question).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.Answer).IsRequired();
        builder.Property(n => n.TargetRowId).HasMaxLength(64);

        // Restrict, not Cascade (pilot-gates plan, phase 1): SQL Server refuses to create an INSTEAD
        // OF UPDATE/DELETE trigger on a table that has an incoming cascading FK, and
        // SharedDraftRevisions needs exactly such a trigger for its own immutability. A revision is
        // never actually deleted by application code (it is superseded/withdrawn, not removed), so
        // this changes no real runtime behavior.
        builder.HasOne(n => n.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(n => n.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: a parent's account is deactivated rather than deleted; never cascade-destroy their notes' FK path unexpectedly.
        builder.HasOne(n => n.ParentUser)
            .WithMany()
            .HasForeignKey(n => n.ParentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The parent's own notes list, scoped to (revision, asking parent) — never a staff route.
        builder.HasIndex(n => new { n.SharedDraftRevisionId, n.ParentUserId });
    }
}
