using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SharedDraftRevisionConfiguration : IEntityTypeConfiguration<SharedDraftRevision>
{
    public void Configure(EntityTypeBuilder<SharedDraftRevision> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ValuesJson).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(1000);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Restrict, not Cascade (pilot-gates plan, phase 1): SQL Server refuses to create an INSTEAD
        // OF UPDATE/DELETE trigger on a table that has ANY cascading FK touching it — including this
        // one, even though SharedDraftRevision is the child side here — and SharedDraftRevisions
        // needs exactly such a trigger for its own immutability (verified against a real SQL Server
        // instance while authoring this migration — error 2113). Unlike the sibling FKs on
        // DraftResponse/ParentDraftNote/etc., a DocumentInstance CAN legitimately be deleted while
        // still Draft (DocumentInstanceService.DeleteAsync) even after being shared — see that
        // method's DbUpdateException handling for the resulting friendly failure instead of a
        // silent cascade-delete of the family's shared history.
        builder.HasOne(r => r.DocumentInstance)
            .WithMany()
            .HasForeignKey(r => r.DocumentInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: the pinned template version must never be deletable while a frozen revision
        // references it — the frozen values are keyed against that exact structure.
        builder.HasOne(r => r.DocumentTemplateVersion)
            .WithMany()
            .HasForeignKey(r => r.DocumentTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SharedByUser)
            .WithMany()
            .HasForeignKey(r => r.SharedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE per instance: RevisionNumber is monotonic (1, 2, 3, …) per DocumentInstanceId.
        builder.HasIndex(r => new { r.DocumentInstanceId, r.RevisionNumber }).IsUnique();
        // The parent reading list + the staff share history both filter/sort by (instance, status).
        builder.HasIndex(r => new { r.DocumentInstanceId, r.Status });
    }
}
