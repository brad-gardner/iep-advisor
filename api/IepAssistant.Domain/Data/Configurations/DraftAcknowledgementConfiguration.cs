using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DraftAcknowledgementConfiguration : IEntityTypeConfiguration<DraftAcknowledgement>
{
    public void Configure(EntityTypeBuilder<DraftAcknowledgement> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(a => a.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotent upsert key: one acknowledgement per (revision, user).
        builder.HasIndex(a => new { a.SharedDraftRevisionId, a.UserId }).IsUnique();
    }
}
