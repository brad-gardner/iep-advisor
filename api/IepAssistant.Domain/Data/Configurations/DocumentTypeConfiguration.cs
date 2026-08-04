using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.HasKey(t => t.Id);
        // Stable, explicit IDs on the seed rows (like OrgRole) so code-side references stay valid.
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Key).HasMaxLength(50).IsRequired();
        builder.Property(t => t.DisplayName).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Key).IsUnique();

        // Seed the lookup table. CreatedAt/UpdatedAt are fixed (not DateTime.UtcNow) so the
        // migration snapshot is deterministic and HasData does not churn on every scaffold.
        var seededAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new DocumentType { Id = 1, Key = "IEP", DisplayName = "IEP", IsActive = true, CreatedAt = seededAt, UpdatedAt = seededAt },
            new DocumentType { Id = 2, Key = "Section504", DisplayName = "Section 504 Plan", IsActive = true, CreatedAt = seededAt, UpdatedAt = seededAt },
            new DocumentType { Id = 3, Key = "ETR", DisplayName = "ETR", IsActive = true, CreatedAt = seededAt, UpdatedAt = seededAt });
    }
}
