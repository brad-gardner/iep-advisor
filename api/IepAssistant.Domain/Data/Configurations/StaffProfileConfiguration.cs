using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(150);
        builder.Property(t => t.Credentials).HasMaxLength(500);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.District)
            .WithMany()
            .HasForeignKey(t => t.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        // SchoolId is nullable (null = DistrictAdmin). Restrict (not Cascade) to avoid a multiple-
        // cascade-path error on SQL Server: School cascades from District, and StaffProfile already
        // has a (Restrict) path to District, so a cascading School delete here would create a second
        // path into StaffProfile. Restrict keeps the FK graph cycle/conflict-free; school deactivation
        // is a soft-delete handled at the service layer (P3).
        builder.HasOne(t => t.School)
            .WithMany()
            .HasForeignKey(t => t.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.OrgRole)
            .WithMany(r => r.StaffProfiles)
            .HasForeignKey(t => t.OrgRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.SchoolId);
        builder.HasIndex(t => t.DistrictId);
        builder.HasIndex(t => t.OrgRoleId);
    }
}
