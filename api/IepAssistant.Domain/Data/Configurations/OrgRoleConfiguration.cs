using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class OrgRoleConfiguration : IEntityTypeConfiguration<OrgRole>
{
    public void Configure(EntityTypeBuilder<OrgRole> builder)
    {
        builder.HasKey(r => r.Id);
        // Stable, explicit IDs (no identity gap on these seed rows) so OrgRoleIds constants stay valid.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        // Seed the lookup table (mirrors OrgRoleIds: 1=DistrictAdmin, 2=SchoolAdmin, 3=Teacher).
        builder.HasData(
            new OrgRole { Id = 1, Name = "DistrictAdmin" },
            new OrgRole { Id = 2, Name = "SchoolAdmin" },
            new OrgRole { Id = 3, Name = "Teacher" });
    }
}
