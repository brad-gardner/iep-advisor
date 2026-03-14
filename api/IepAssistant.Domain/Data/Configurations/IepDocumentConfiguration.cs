using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDocumentConfiguration : IEntityTypeConfiguration<IepDocument>
{
    public void Configure(EntityTypeBuilder<IepDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.FileName).HasMaxLength(500).IsRequired();
        builder.Property(d => d.BlobUri).HasMaxLength(2000).IsRequired();
        builder.Property(d => d.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(d => d.ChildProfile)
            .WithMany()
            .HasForeignKey(d => d.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.ChildProfileId);
    }
}
