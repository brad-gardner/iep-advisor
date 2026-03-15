using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ChildAccessConfiguration : IEntityTypeConfiguration<ChildAccess>
{
    public void Configure(EntityTypeBuilder<ChildAccess> builder)
    {
        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ca => ca.InviteEmail).HasMaxLength(256);
        builder.Property(ca => ca.InviteToken).HasMaxLength(128);

        builder.HasOne(ca => ca.ChildProfile)
            .WithMany()
            .HasForeignKey(ca => ca.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ca => ca.User)
            .WithMany()
            .HasForeignKey(ca => ca.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ca => ca.ChildProfileId);
        builder.HasIndex(ca => ca.UserId);
        builder.HasIndex(ca => ca.InviteToken);

        builder.HasIndex(ca => new { ca.ChildProfileId, ca.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");
    }
}
