using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class GoalObservationConfiguration : IEntityTypeConfiguration<GoalObservation>
{
    public void Configure(EntityTypeBuilder<GoalObservation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Value).HasColumnType("decimal(18,4)");
        builder.Property(o => o.Unit).HasMaxLength(32);
        builder.Property(o => o.Note).HasMaxLength(2000);

        builder.HasOne(o => o.GoalRecord)
            .WithMany(g => g.Observations)
            .HasForeignKey(o => o.GoalRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.GoalRecordId, o.ObservedAt });
    }
}
