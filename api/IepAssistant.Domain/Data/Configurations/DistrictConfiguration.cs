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

        // Explicit SQL-side DEFAULT (true) so existing districts are backfilled enabled by the migration,
        // not just newly-constructed C# entities (plan 6, decision 2).
        builder.Property(d => d.FamilyDraftSharingEnabled).HasDefaultValue(true);

        // Pilot-gates plan, phase 3: explicit SQL-side defaults so existing districts backfill correctly.
        builder.Property(d => d.IsDemo).HasDefaultValue(false);
        builder.Property(d => d.MagicLinkEnabled).HasDefaultValue(true);
        builder.Property(d => d.RequireMfaForMagicLink).HasDefaultValue(true);

        builder.HasMany(d => d.Schools)
            .WithOne(s => s.District)
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
