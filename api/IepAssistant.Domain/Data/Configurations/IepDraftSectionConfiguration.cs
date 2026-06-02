using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftSectionConfiguration : IEntityTypeConfiguration<IepDraftSection>
{
    public void Configure(EntityTypeBuilder<IepDraftSection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SectionKind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(s => s.IepDraft)
            .WithMany(d => d.Sections)
            .HasForeignKey(s => s.IepDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IepDraftId);
        builder.HasIndex(s => s.LineageId);
    }
}
