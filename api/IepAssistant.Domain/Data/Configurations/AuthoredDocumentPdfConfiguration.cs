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

        // Cascade: the PDF tracking row is owned content of its version.
        builder.HasOne(p => p.AuthoredDocumentVersion)
            .WithOne(v => v.Pdf)
            .HasForeignKey<AuthoredDocumentPdf>(p => p.AuthoredDocumentVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.AuthoredDocumentVersionId).IsUnique();
    }
}
