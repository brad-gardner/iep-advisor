using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class GoalRetirementConfiguration : IEntityTypeConfiguration<GoalRetirement>
{
    public void Configure(EntityTypeBuilder<GoalRetirement> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(1000);

        builder.HasOne(r => r.DocumentInstance)
            .WithMany()
            .HasForeignKey(r => r.DocumentInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // The finalize projection looks up "does a retirement exist for (instance, lineage)" — one key.
        builder.HasIndex(r => new { r.DocumentInstanceId, r.LineageId });
    }
}
