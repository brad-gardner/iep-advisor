using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionSectionConfiguration : IEntityTypeConfiguration<IepVersionSection>
{
    public void Configure(EntityTypeBuilder<IepVersionSection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SectionKind)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.HasOne(s => s.IepVersion)
            .WithMany(v => v.Sections)
            .HasForeignKey(s => s.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IepVersionId);
        // LineageId index backs the cross-version lineage query (version children -> IepVersion -> student).
        builder.HasIndex(s => s.LineageId);
    }
}
