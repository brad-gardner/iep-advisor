using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionAccommodationConfiguration : IEntityTypeConfiguration<IepVersionAccommodation>
{
    public void Configure(EntityTypeBuilder<IepVersionAccommodation> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Category).HasMaxLength(150);

        builder.HasOne(a => a.IepVersion)
            .WithMany(v => v.Accommodations)
            .HasForeignKey(a => a.IepVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.IepVersionId);
        builder.HasIndex(a => a.LineageId);
    }
}
