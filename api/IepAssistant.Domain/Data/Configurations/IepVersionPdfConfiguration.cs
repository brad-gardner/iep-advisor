using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionPdfConfiguration : IEntityTypeConfiguration<IepVersionPdf>
{
    public void Configure(EntityTypeBuilder<IepVersionPdf> builder)
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

        // Multilingual plan phase 7: one-to-many (was one-to-one) — a version can now have one rendered
        // PDF row per language.
        builder.HasOne(p => p.IepVersion)
            .WithMany(v => v.Pdfs)
            .HasForeignKey(p => p.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite, not single-column: English and Spanish renders of the same version are separate rows.
        // EF Core's SQL Server convention adds a "[Language] IS NOT NULL" filter to a unique index over a
        // nullable column automatically (see the generated AddPdfLanguage migration) — pre-phase-7 rows
        // (Language still NULL) are therefore exempt from this constraint entirely, and only rows with an
        // explicit Language (every row created after this phase, English included) are deduplicated by it.
        builder.HasIndex(p => new { p.IepVersionId, p.Language }).IsUnique();
    }
}
