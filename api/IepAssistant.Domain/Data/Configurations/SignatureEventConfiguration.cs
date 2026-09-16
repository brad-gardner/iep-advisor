using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

/// <summary>STUB configuration for the future e-sign <see cref="SignatureEvent"/> table (plan 7, decision 4) — no service reads/writes this today.</summary>
public class SignatureEventConfiguration : IEntityTypeConfiguration<SignatureEvent>
{
    public void Configure(EntityTypeBuilder<SignatureEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SignerName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.SignerRole).HasMaxLength(100);
        builder.Property(e => e.Method).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(e => e.AuthoredDocumentVersion)
            .WithMany()
            .HasForeignKey(e => e.AuthoredDocumentVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.AuthoredDocumentVersionId);
    }
}
