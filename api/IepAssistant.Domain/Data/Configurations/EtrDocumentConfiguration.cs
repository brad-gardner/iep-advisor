using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class EtrDocumentConfiguration : IEntityTypeConfiguration<EtrDocument>
{
    public void Configure(EntityTypeBuilder<EtrDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.FileName).HasMaxLength(500);
        builder.Property(d => d.BlobUri).HasMaxLength(2000);
        builder.Property(d => d.Status).HasMaxLength(50).IsRequired();
        builder.Property(d => d.DocumentState).HasMaxLength(50).IsRequired();
        builder.Property(d => d.EvaluationType).HasMaxLength(50);
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.HasOne(d => d.ChildProfile)
            .WithMany()
            .HasForeignKey(d => d.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.ChildProfileId);
    }
}
