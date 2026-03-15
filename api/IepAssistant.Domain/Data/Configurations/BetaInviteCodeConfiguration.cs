using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class BetaInviteCodeConfiguration : IEntityTypeConfiguration<BetaInviteCode>
{
    public void Configure(EntityTypeBuilder<BetaInviteCode> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(b => b.RedeemedBy)
            .WithMany()
            .HasForeignKey(b => b.RedeemedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.Code).IsUnique();
        builder.HasIndex(b => b.RedeemedByUserId);
    }
}
