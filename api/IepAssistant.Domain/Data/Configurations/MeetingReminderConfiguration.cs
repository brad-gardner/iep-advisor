using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingReminderConfiguration : IEntityTypeConfiguration<MeetingReminder>
{
    public void Configure(EntityTypeBuilder<MeetingReminder> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Offset).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(r => r.Meeting)
            .WithMany()
            .HasForeignKey(r => r.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Required by deliverable A: the sole idempotency guard for the reminder worker/service.
        builder.HasIndex(r => new { r.MeetingId, r.UserId, r.Offset }).IsUnique();
    }
}
