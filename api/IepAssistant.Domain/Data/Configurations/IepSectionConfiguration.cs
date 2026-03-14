using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepSectionConfiguration : IEntityTypeConfiguration<IepSection>
{
    public void Configure(EntityTypeBuilder<IepSection> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SectionType).HasMaxLength(50).IsRequired();

        builder.HasOne(s => s.IepDocument)
            .WithMany()
            .HasForeignKey(s => s.IepDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IepDocumentId);
    }
}
