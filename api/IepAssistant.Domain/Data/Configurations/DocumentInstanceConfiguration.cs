using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DocumentInstanceConfiguration : IEntityTypeConfiguration<DocumentInstance>
{
    public void Configure(EntityTypeBuilder<DocumentInstance> builder)
    {
        builder.HasKey(i => i.Id);

        // Enum stored as a string (mirrors IepDraft.Status + JsonStringEnumConverter).
        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // The value-document. Required; defaults to an empty object. A max size is enforced in the
        // service (see DocumentInstanceService.MaxValuesJsonBytes, cross-cutting G-x.2) rather than as a
        // column length so the friendly error surfaces before the write.
        builder.Property(i => i.ValuesJson).IsRequired();

        // Optimistic-concurrency token for the whole value-document. A plain (non-store-generated)
        // concurrency token rotated by the service on every save — chosen over a store-generated
        // rowversion so it advances deterministically under BOTH SQL Server and the SQLite test
        // provider (the same rationale as DocumentTemplateVersion.RowVersion). EF still adds it to the
        // UPDATE WHERE clause, giving DB-level protection against a concurrent write.
        builder.Property(i => i.RowVersion).IsConcurrencyToken();

        // Cascade: an instance is owned content of a student (mirrors IepDraft → SchoolStudent).
        builder.HasOne(i => i.SchoolStudent)
            .WithMany()
            .HasForeignKey(i => i.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: DocumentType is a lookup row referenced by many instances; never cascade-destroy it.
        builder.HasOne(i => i.DocumentType)
            .WithMany()
            .HasForeignKey(i => i.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: a pinned template version must NOT be deletable while instances reference it
        // (G-b.4) — the pinned structure is what the stored values are keyed against.
        builder.HasOne(i => i.DocumentTemplateVersion)
            .WithMany()
            .HasForeignKey(i => i.DocumentTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.SchoolStudentId);
        builder.HasIndex(i => new { i.SchoolStudentId, i.DocumentTypeId });
    }
}
