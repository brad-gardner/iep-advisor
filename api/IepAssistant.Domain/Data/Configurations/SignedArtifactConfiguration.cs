using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SignedArtifactConfiguration : IEntityTypeConfiguration<SignedArtifact>
{
    public void Configure(EntityTypeBuilder<SignedArtifact> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.BlobPath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(260);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.SignerSummary).HasMaxLength(500);

        builder.HasOne(a => a.AuthoredDocumentVersion)
            .WithMany(v => v.SignedArtifacts)
            .HasForeignKey(a => a.AuthoredDocumentVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AuthoredDocumentVersionId);
    }
}
