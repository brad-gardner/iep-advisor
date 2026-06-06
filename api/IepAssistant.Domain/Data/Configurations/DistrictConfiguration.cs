using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.StateCode).HasMaxLength(2);

        builder.HasMany(d => d.Schools)
            .WithOne(s => s.District)
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
