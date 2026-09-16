using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingSummaryConfiguration : IEntityTypeConfiguration<MeetingSummary>
{
    public void Configure(EntityTypeBuilder<MeetingSummary> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Body).HasMaxLength(8000).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(s => s.Meeting)
            .WithMany()
            .HasForeignKey(s => s.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 per meeting.
        builder.HasIndex(s => s.MeetingId).IsUnique();
    }
}
