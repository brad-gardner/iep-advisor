using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(b => b.FileName).HasMaxLength(260).IsRequired();

        builder.HasOne(b => b.District)
            .WithMany()
            .HasForeignKey(b => b.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Rows)
            .WithOne(r => r.Batch)
            .HasForeignKey(r => r.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.DistrictId, b.CreatedAt });
    }
}
