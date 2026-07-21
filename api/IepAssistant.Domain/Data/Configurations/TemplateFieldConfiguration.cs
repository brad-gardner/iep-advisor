using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    public void Configure(EntityTypeBuilder<TemplateField> builder)
    {
        builder.HasKey(f => f.Id);

        // Enum stored as a string (mirrors TemplateVersionStatus + JsonStringEnumConverter).
        builder.Property(f => f.FieldType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Label).HasMaxLength(300).IsRequired();

        // Section relationship (Cascade) is declared on the section side. The version relationship here
        // uses the denormalized DocumentTemplateVersionId and is NoAction to avoid multiple cascade
        // paths into TemplateField (version -> section -> field is the single cascade path).
        builder.HasOne(f => f.Version)
            .WithMany()
            .HasForeignKey(f => f.DocumentTemplateVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(f => f.TemplateSectionId);
        builder.HasIndex(f => f.DocumentTemplateVersionId);

        // FieldKey is stable and unique WITHIN a version (instance values are keyed by it). Carried
        // verbatim across forks, so it is version-scoped rather than globally unique.
        builder.HasIndex(f => new { f.DocumentTemplateVersionId, f.FieldKey }).IsUnique();
    }
}
