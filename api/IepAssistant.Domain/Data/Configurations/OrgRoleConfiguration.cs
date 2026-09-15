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

        // Seed the lookup table (mirrors OrgRoleIds: 1=DistrictAdmin, 2=SchoolAdmin, 3=Teacher,
        // 4=RelatedServiceProvider, 5=GeneralEducator — the last two share Teacher's authz tier; plan 3).
        builder.HasData(
            new OrgRole { Id = 1, Name = "DistrictAdmin" },
            new OrgRole { Id = 2, Name = "SchoolAdmin" },
            new OrgRole { Id = 3, Name = "Teacher" },
            new OrgRole { Id = 4, Name = "RelatedServiceProvider" },
            new OrgRole { Id = 5, Name = "GeneralEducator" });
    }
}
