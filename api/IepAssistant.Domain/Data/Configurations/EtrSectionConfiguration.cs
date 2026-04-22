using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class EtrSectionConfiguration : IEntityTypeConfiguration<EtrSection>
{
    public void Configure(EntityTypeBuilder<EtrSection> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SectionType).HasMaxLength(50).IsRequired();

        builder.HasOne(s => s.EtrDocument)
            .WithMany()
            .HasForeignKey(s => s.EtrDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.EtrDocumentId);
    }
}
