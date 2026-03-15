using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class KnowledgeBaseEntryConfiguration : IEntityTypeConfiguration<KnowledgeBaseEntry>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Content).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(50).IsRequired();
        builder.Property(e => e.LegalReference).HasMaxLength(100);
        builder.Property(e => e.State).HasMaxLength(10);
        builder.Property(e => e.Tags).HasMaxLength(500);

        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.State);
    }
}
