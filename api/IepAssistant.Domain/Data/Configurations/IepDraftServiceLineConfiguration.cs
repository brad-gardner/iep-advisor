using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftServiceLineConfiguration : IEntityTypeConfiguration<IepDraftServiceLine>
{
    public void Configure(EntityTypeBuilder<IepDraftServiceLine> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ServiceType).HasMaxLength(200);
        builder.Property(s => s.Frequency).HasMaxLength(150);
        builder.Property(s => s.Duration).HasMaxLength(150);
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.ProviderRole).HasMaxLength(150);

        builder.HasOne(s => s.IepDraft)
            .WithMany(d => d.ServiceLines)
            .HasForeignKey(s => s.IepDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.IepDraftId);
        builder.HasIndex(s => s.LineageId);
    }
}
