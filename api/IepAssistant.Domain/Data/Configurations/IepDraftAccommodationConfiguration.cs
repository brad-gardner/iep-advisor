using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftAccommodationConfiguration : IEntityTypeConfiguration<IepDraftAccommodation>
{
    public void Configure(EntityTypeBuilder<IepDraftAccommodation> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Category).HasMaxLength(150);

        builder.HasOne(a => a.IepDraft)
            .WithMany(d => d.Accommodations)
            .HasForeignKey(a => a.IepDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.IepDraftId);
        builder.HasIndex(a => a.LineageId);
    }
}
