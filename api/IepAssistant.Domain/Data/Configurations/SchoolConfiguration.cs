using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.StateCode).HasMaxLength(2);

        builder.HasOne(s => s.District)
            .WithMany(d => d.Schools)
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Students)
            .WithOne(st => st.School)
            .HasForeignKey(st => st.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.DistrictId);
    }
}
