using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AuthoredDocumentPdfConfiguration : IEntityTypeConfiguration<AuthoredDocumentPdf>
{
    public void Configure(EntityTypeBuilder<AuthoredDocumentPdf> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.RenderStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Checksum).HasMaxLength(128);
        builder.Property(p => p.ErrorMessage).HasMaxLength(2000);
        // Multilingual plan phase 7: nullable, "en"/"es" — see the property doc comment for why null
        // (pre-phase-7 rows) means English rather than being backfilled.
        builder.Property(p => p.Language).HasMaxLength(10);

        // Cascade: the PDF tracking row is owned content of its version. One-to-many (was one-to-one,
        // multilingual plan phase 7) — a version can now have one rendered PDF row per language.
        builder.HasOne(p => p.AuthoredDocumentVersion)
            .WithMany(v => v.Pdfs)
            .HasForeignKey(p => p.AuthoredDocumentVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite, not single-column: English and Spanish renders of the same version are separate rows.
        // See IepVersionPdfConfiguration for why NULL Language values (every pre-phase-7 row) are exempt
        // from this constraint — EF's SQL Server convention filters them out of the unique index.
        builder.HasIndex(p => new { p.AuthoredDocumentVersionId, p.Language }).IsUnique();
    }
}
