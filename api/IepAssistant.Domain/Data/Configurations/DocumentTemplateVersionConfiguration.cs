using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DocumentTemplateVersionConfiguration : IEntityTypeConfiguration<DocumentTemplateVersion>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);

        // Enum stored as a string (mirrors IepVersion.DocumentType + JsonStringEnumConverter).
        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Optimistic-concurrency token for the working copy. A plain (non-store-generated) concurrency
        // token rotated by the authoring service on every edit — chosen over a store-generated
        // rowversion so the token advances deterministically under BOTH SQL Server and the SQLite test
        // provider (SQLite never auto-populates rowversion, which would make concurrency untestable).
        // EF still adds it to the UPDATE WHERE clause, giving DB-level protection against a concurrent
        // write that lands between the service's load and save.
        builder.Property(v => v.RowVersion).IsConcurrencyToken();

        // Sections are owned content of a version.
        builder.HasMany(v => v.Sections)
            .WithOne(s => s.DocumentTemplateVersion)
            .HasForeignKey(s => s.DocumentTemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade: versions are owned content of a template; deleting a template removes its versions.
        builder.HasOne(v => v.DocumentTemplate)
            .WithMany(t => t.Versions)
            .HasForeignKey(v => v.DocumentTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.DocumentTemplateId);

        // UNIQUE backstop for monotonic VersionNumber per template (mirrors the IepVersion
        // (SchoolStudentId, VersionNumber) precedent). Also closes the fork read-then-insert race:
        // two concurrent CreateDraftFromPublished calls both compute the same next number and the
        // loser fails with a unique violation (translated to a friendly error) rather than creating a
        // duplicate/second Draft. Provider-portable (no filter) so it holds under SQLite tests too.
        builder.HasIndex(v => new { v.DocumentTemplateId, v.VersionNumber }).IsUnique();
    }
}
