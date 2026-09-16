using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.LinkPath).HasMaxLength(300);
        builder.Property(n => n.DedupKey).HasMaxLength(200).IsRequired();
        builder.Property(n => n.EmailError).HasMaxLength(500);

        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Required by deliverable A: the bell/unread-count read path.
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        // Required by deliverable A: NotifyAsync's 24h dedup lookup.
        builder.HasIndex(n => new { n.UserId, n.Kind, n.DedupKey, n.CreatedAt });
        // Additive: NotificationEmailWorker's drain query filters on exactly these three columns.
        builder.HasIndex(n => new { n.EmailQueuedAt, n.EmailSentAt, n.EmailAttempts });
    }
}
