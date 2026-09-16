using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MagicLinkTokenConfiguration : IEntityTypeConfiguration<MagicLinkToken>
{
    public void Configure(EntityTypeBuilder<MagicLinkToken> builder)
    {
        builder.HasKey(t => t.Id);
        // Base64 SHA-256 digest is 44 chars; cap at 88 matching StaffInvite/ChildLink token-hash sizing.
        builder.Property(t => t.TokenHash).HasMaxLength(88).IsRequired();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lookup on consume (hash is effectively unique per live token; a prior used/expired row with the
        // same hash is astronomically unlikely given 32 random bytes, so a plain index — not unique — is
        // enough and avoids a theoretical collision blocking a legitimate insert).
        builder.HasIndex(t => t.TokenHash);

        // Rate-limit scan: "how many tokens has this user requested in the last 15 minutes".
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });
    }
}
