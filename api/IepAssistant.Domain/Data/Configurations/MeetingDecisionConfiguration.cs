using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingDecisionConfiguration : IEntityTypeConfiguration<MeetingDecision>
{
    public void Configure(EntityTypeBuilder<MeetingDecision> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Text).IsRequired().HasMaxLength(2000);
        builder.Property(d => d.TargetRowId).HasMaxLength(64);
        builder.Property(d => d.TargetLabel).HasMaxLength(500);
        builder.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(d => d.Meeting)
            .WithMany()
            .HasForeignKey(d => d.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.MeetingId);
        builder.HasIndex(d => d.AppliedAt);
    }
}
