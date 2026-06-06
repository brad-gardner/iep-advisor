using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionServiceLineConfiguration : IEntityTypeConfiguration<IepVersionServiceLine>
{
    public void Configure(EntityTypeBuilder<IepVersionServiceLine> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ServiceType).HasMaxLength(200);
        builder.Property(s => s.Frequency).HasMaxLength(150);
        builder.Property(s => s.Duration).HasMaxLength(150);
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.ProviderRole).HasMaxLength(150);

        builder.HasOne(s => s.IepVersion)
            .WithMany(v => v.ServiceLines)
            .HasForeignKey(s => s.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IepVersionId);
        builder.HasIndex(s => s.LineageId);
    }
}
