using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DocumentTemplateVersionConfiguration : IEntityTypeConfiguration<DocumentTemplateVersion>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);

        // Enum stored as a string (mirrors IepVersion.DocumentType + JsonStringEnumConverter).
        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Optimistic-concurrency token for the working copy (SQL Server rowversion).
        builder.Property(v => v.RowVersion).IsRowVersion();

        // Cascade: versions are owned content of a template; deleting a template removes its versions.
        builder.HasOne(v => v.DocumentTemplate)
            .WithMany(t => t.Versions)
            .HasForeignKey(v => v.DocumentTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.DocumentTemplateId);
    }
}
