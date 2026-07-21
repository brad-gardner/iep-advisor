using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AuthoredDocumentVersionConfiguration : IEntityTypeConfiguration<AuthoredDocumentVersion>
{
    public void Configure(EntityTypeBuilder<AuthoredDocumentVersion> builder)
    {
        builder.HasKey(v => v.Id);

        // The frozen value-document. Required; a max size is enforced upstream on the mutable
        // DocumentInstance (DocumentInstanceService.MaxValuesJsonBytes) so it can never grow here.
        builder.Property(v => v.ValuesJson).IsRequired();

        // Restrict (not Cascade): a finalized AuthoredDocumentVersion is an immutable legal record and
        // must not be silently destroyed by deleting its SchoolStudent (mirrors IepVersion).
        builder.HasOne(v => v.SchoolStudent)
            .WithMany()
            .HasForeignKey(v => v.SchoolStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: DocumentType is a lookup row referenced by many versions; never cascade-destroy it.
        builder.HasOne(v => v.DocumentType)
            .WithMany()
            .HasForeignKey(v => v.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: the pinned template version must never be deletable while a finalized snapshot
        // references it — the frozen values are keyed against that exact structure.
        builder.HasOne(v => v.DocumentTemplateVersion)
            .WithMany()
            .HasForeignKey(v => v.DocumentTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE backstop for monotonic VersionNumber per (student, docType). FinalizeAsync's
        // serializable transaction prevents the read-then-insert race on SQL Server; this index
        // guarantees the invariant even if two finalizes slip through (the loser fails with a unique
        // violation and rolls back). Also backs per-tuple listing + next-number. Scoped per
        // (student, docType) so IEP and ETR number INDEPENDENTLY for the same student.
        builder.HasIndex(v => new { v.SchoolStudentId, v.DocumentTypeId, v.VersionNumber }).IsUnique();
    }
}
