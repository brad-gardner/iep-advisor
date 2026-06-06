using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionPdfConfiguration : IEntityTypeConfiguration<IepVersionPdf>
{
    public void Configure(EntityTypeBuilder<IepVersionPdf> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.RenderStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Checksum).HasMaxLength(128);
        builder.Property(p => p.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(p => p.IepVersion)
            .WithOne(v => v.Pdf)
            .HasForeignKey<IepVersionPdf>(p => p.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.IepVersionId).IsUnique();
    }
}
