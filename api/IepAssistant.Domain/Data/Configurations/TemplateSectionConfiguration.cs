using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class TemplateSectionConfiguration : IEntityTypeConfiguration<TemplateSection>
{
    public void Configure(EntityTypeBuilder<TemplateSection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();

        // Cascade: sections are owned content of a version (relationship also declared on the version
        // side; declared once there). Fields cascade from their section below.
        builder.HasMany(s => s.Fields)
            .WithOne(f => f.Section)
            .HasForeignKey(f => f.TemplateSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.DocumentTemplateVersionId);

        // SectionKey is stable and unique WITHIN a version (carried verbatim across forks, so it is NOT
        // globally unique — the same key exists in the forked published + draft versions).
        builder.HasIndex(s => new { s.DocumentTemplateVersionId, s.SectionKey }).IsUnique();
    }
}
