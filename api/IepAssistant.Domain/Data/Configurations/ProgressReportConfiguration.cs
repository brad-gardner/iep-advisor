using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.FileName).HasMaxLength(500);
        builder.Property(p => p.BlobUri).HasMaxLength(2000);
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.Property(p => p.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(p => p.IepDocument)
            .WithMany()
            .HasForeignKey(p => p.IepDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction to avoid SQL Server cascade-cycle (IepDocument cascades from
        // ChildProfile, and ProgressReport cascades from IepDocument).
        builder.HasOne(p => p.ChildProfile)
            .WithMany()
            .HasForeignKey(p => p.ChildProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(p => p.IepDocumentId);
        builder.HasIndex(p => p.ChildProfileId);
    }
}
