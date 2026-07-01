using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StaffInviteConfiguration : IEntityTypeConfiguration<StaffInvite>
{
    public void Configure(EntityTypeBuilder<StaffInvite> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).HasMaxLength(256);
        // Base64 SHA-256 digest is 44 chars; cap at 88 to match the existing invite-hash column sizing.
        builder.Property(i => i.InviteToken).HasMaxLength(88);

        // Non-unique index per the existing StudentInvite/ChildLink pattern: tokens are nulled on accept
        // so a unique index would collide on multiple claimed (null) rows; the lookup is hash + active.
        builder.HasIndex(i => i.InviteToken);

        // Filtered UNIQUE index on Email — DB backstop for the "one live invite per email" rule (the
        // service AnyAsync pre-check is TOCTOU and can't stop a concurrent insert). The filter scopes
        // uniqueness to LIVE pending invites only (active, unaccepted, token still present), so revoked
        // (IsActive=0 / token nulled) and accepted (AcceptedAt set / token nulled) rows free the slot and
        // never collide. Filter is intentionally written WITHOUT bracket quoting so it is portable across
        // SQL Server (partial index) AND SQLite (EnsureCreated carries the same string for the test DB).
        builder.HasIndex(i => i.Email)
            .IsUnique()
            .HasFilter("IsActive = 1 AND AcceptedAt IS NULL AND InviteToken IS NOT NULL");
        builder.HasIndex(i => i.DistrictId);
        builder.HasIndex(i => i.SchoolId);

        // All FKs Restrict (no multiple-cascade-path on SQL Server; org rows are soft-deactivated, not deleted).
        builder.HasOne(i => i.District)
            .WithMany()
            .HasForeignKey(i => i.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.School)
            .WithMany()
            .HasForeignKey(i => i.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.OrgRole)
            .WithMany()
            .HasForeignKey(i => i.OrgRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
