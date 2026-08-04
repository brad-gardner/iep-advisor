using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        // Normalized 2-letter uppercase (service-enforced) or null for the default template.
        builder.Property(t => t.StateCode).HasMaxLength(2);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();

        // Restrict: a DocumentType is a lookup row that may be referenced by many templates; it must
        // not be silently destroyed (and templates carry no student PII to cascade away).
        builder.HasOne(t => t.DocumentType)
            .WithMany(dt => dt.Templates)
            .HasForeignKey(t => t.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE identity of a template: exactly one template per (state, documentType), enforced as
        // a DB backstop (the service pre-checks for a friendly error). HasFilter(null) makes the
        // "no filter" intent explicit so the constraint also covers the default template (null
        // StateCode) — on SQL Server NULLs compare equal, giving one default per DocumentType.
        builder.HasIndex(t => new { t.StateCode, t.DocumentTypeId })
            .IsUnique()
            .HasFilter(null);
    }
}
