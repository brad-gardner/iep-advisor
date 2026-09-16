using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingBriefConfiguration : IEntityTypeConfiguration<MeetingBrief>
{
    public void Configure(EntityTypeBuilder<MeetingBrief> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BriefJson).IsRequired();

        builder.HasOne(b => b.Meeting)
            .WithMany()
            .HasForeignKey(b => b.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 per meeting — regenerate replaces this row in place.
        builder.HasIndex(b => b.MeetingId).IsUnique();
    }
}
