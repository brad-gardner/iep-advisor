using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(256).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(1000);
        builder.Property(r => r.ChangesJson).IsRequired();
        builder.Property(r => r.PayloadJson).IsRequired();

        builder.HasIndex(r => new { r.BatchId, r.RowNumber }).IsUnique();
    }
}
