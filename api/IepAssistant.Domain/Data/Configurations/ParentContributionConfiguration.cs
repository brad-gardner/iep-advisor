using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ParentContributionConfiguration : IEntityTypeConfiguration<ParentContribution>
{
    public void Configure(EntityTypeBuilder<ParentContribution> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Text).HasMaxLength(2000).IsRequired();

        builder.HasOne(c => c.ChildProfile)
            .WithMany()
            .HasForeignKey(c => c.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ChildProfileId, c.IsShared });
    }
}
