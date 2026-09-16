using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ExternalName).HasMaxLength(200);
        builder.Property(p => p.ExternalEmail).HasMaxLength(256);
        builder.Property(p => p.ExcusalNote).HasMaxLength(500);
        builder.Property(p => p.RsvpToken).HasMaxLength(32).IsRequired();

        builder.Property(p => p.TeamRole).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(p => p.InviteStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(p => p.Meeting)
            .WithMany(m => m.Participants)
            .HasForeignKey(p => p.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.MeetingId);
        builder.HasIndex(p => p.UserId);
        // Required by deliverable A: RsvpToken is unique (backs the no-login email RSVP link/lookup).
        builder.HasIndex(p => p.RsvpToken).IsUnique();
    }
}
